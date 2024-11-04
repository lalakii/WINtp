using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;

[assembly: AssemblyProduct("WINtp")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]
[assembly: AssemblyTitle("简单好用的时间同步小工具")]
[assembly: AssemblyCopyright("Copyright (C) 2024 lalaki.cn")]

[DesignerCategory("")]
public class WINtp : ServiceBase
{
    private static readonly string exePath = typeof(WINtp).Assembly.Location;
    private static readonly ManualResetEvent evt = new(false);
    private CancellationTokenSource cts;
    private bool autoSyncTime = false;
    private bool verbose = false;
    private static SystemTime st;

    protected override void OnStart(string[] args)
    {
        ThreadPool.QueueUserWorkItem(InitProfile, args);
    }

    public void InitProfile(object args)
    {
        List<string> NtpServer = ["time.asia.apple.com", "time.windows.com", "rhel.pool.ntp.org"];
        var cfgPath = Path.Combine(Path.GetDirectoryName(exePath), "ntp.ini");
        var useDefaultNtpServer = true;
        if (File.Exists(cfgPath))
        {
            foreach (var item in File.ReadAllLines(cfgPath))
            {
                var srv = (item + "").Trim().Replace(" ", "").ToLower();
                if (StartsWithoutComment(srv))
                {
                    if (srv.Contains("="))
                    {
                        if (srv.Contains("usedefaultntpserver=false"))
                        {
                            useDefaultNtpServer = false;
                        }
                        else if (srv.Contains("autosynctime=true"))
                        {
                            autoSyncTime = true;
                        }
                        else if (srv.Contains("verbose=true"))
                        {
                            verbose = true;
                        }
                    }
                    else
                    {
                        if (!NtpServer.Contains(srv))
                        {
                            NtpServer.Add(srv);
                        }
                    }
                }
            }
        }
        if (!useDefaultNtpServer && NtpServer.Count > 3)
        {
            NtpServer.RemoveRange(0, 3);
        }
        using (cts = new CancellationTokenSource())
        {
            NtpServer.ForEach(srv => ThreadPool.QueueUserWorkItem(GetNtpTime, srv));
            if (!evt.WaitOne(10000))
            {
                Environment.FailFast(exePath + " 同步时间失败，网络不畅通。");
            }
            if (args != null)
            {
                Thread.Sleep(3000);
                Stop();
            }
        }
    }

    public static void Main(string[] args)
    {
        var svc = new WINtp();
        if (args != null && args.Contains("-k", StringComparer.OrdinalIgnoreCase))
        {
            Run(svc);
        }
        else
        {
            svc.InitProfile(null);
        }
    }

    public static bool StartsWithoutComment(string str)
    {
        return str != "" && !new char[] { '-', '#', '/', ';', '<', '=', ':' }.Contains(str.First());
    }

    // stackoverflow.com/a/3294698/162671
    private static ulong SwapEndianness(byte[] data, int index)
    {
        ulong x = BitConverter.ToUInt32(data, index);
        return (((x & 0x000000ff) << 24) +
                       ((x & 0x0000ff00) << 8) +
                       ((x & 0x00ff0000) >> 8) +
                       ((x & 0xff000000) >> 24)) * 1000;
    }

    private void GetNtpTime(object server)
    {
        var date = new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        var data = new byte[48];
        data[0] = 0x1B;
        try
        {
            using Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.SendTimeout = socket.ReceiveTimeout = 5000;
            socket.Connect(new IPEndPoint(Dns.GetHostEntry((string)server).AddressList.ElementAtOrDefault(0), 123));
            socket.Send(data);
            socket.Receive(data);
            date = date.AddMilliseconds(SwapEndianness(data, 40) + (SwapEndianness(data, 44) / 0x100000000L));
        }
        catch
        {
            return;
        }
        if (!cts.IsCancellationRequested)
        {
            cts.Cancel();
            st.wYear = (short)date.Year;
            st.wMonth = (short)date.Month;
            st.wDay = (short)date.Day;
            st.wHour = (short)date.Hour;
            st.wMinute = (short)date.Minute;
            st.wSecond = (short)date.Second;
            if (autoSyncTime)
            {
                SetSystemTime(ref st);
            }
            if (verbose)
            {
                var msg = string.Format("/c echo Get Datetime from \"{0}\", {1} && pause", server, date.AddHours(TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).Hours));
                try
                {
                    Process.Start("cmd.exe", msg);
                }
                catch
                {
                    Environment.FailFast(exePath + " 无法输出详细信息，请检查此电脑的 PATH 环境变量。");
                }
            }
            evt.Set();
        }
    }

    // stackoverflow.com/questions/650849#answer-650872
    [DllImport("kernel32.dll")]
    private static extern bool SetSystemTime(ref SystemTime time);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemTime
    {
        public short wYear;
        public short wMonth;
        public short wDayOfWeek;
        public short wDay;
        public short wHour;
        public short wMinute;
        public short wSecond;
        public short wMilliseconds;
    }
}