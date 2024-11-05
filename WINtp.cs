using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

[assembly: AssemblyProduct("WINtp")]
[assembly: AssemblyVersion("1.3.0.0")]
[assembly: AssemblyFileVersion("1.3.0.0")]
[assembly: AssemblyTitle("简单好用的时间同步小工具")]
[assembly: AssemblyCopyright("Copyright (C) 2024 lalaki.cn")]

[System.ComponentModel.DesignerCategory("")]
public class WINtp : System.ServiceProcess.ServiceBase
{
    private static readonly Assembly asm = typeof(WINtp).Assembly;
    private static readonly ManualResetEvent evt = new(false);
    private CancellationTokenSource cts;
    private bool autoSyncTime = false;
    private static SystemTime st;
    private bool verbose = false;

    public static void Main(string[] args)
    {
        using var ntp = new WINtp();
        if (args != null && args.Contains("-k", StringComparer.OrdinalIgnoreCase))
        {
            Run(ntp);
        }
        else
        {
            ntp.LoadProfile(null);
        }
    }

    public void LoadProfile(object args)
    {
        var timeout = 0;
        var useDefaultNtpServer = true;
        var cfgPath = Path.Combine(Path.GetDirectoryName(asm.Location), "ntp.ini");
        List<string> ntpServers = ["time.asia.apple.com", "time.windows.com", "rhel.pool.ntp.org"];
        var hasCfg = File.Exists(cfgPath);
        if (hasCfg)
        {
            File.SetAttributes(cfgPath, File.GetAttributes(cfgPath) & ~FileAttributes.ReadOnly);
        }
        var cfgStream = new FileStream(cfgPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        if (!hasCfg)
        {
            var res = asm.GetManifestResourceNames();
            var defaultCfg = asm.GetManifestResourceStream(res.FirstOrDefault());
            defaultCfg.CopyTo(cfgStream);
            cfgStream.Position = 0;
        }
        var reader = new StreamReader(cfgStream);
        while (reader.Peek() != -1)
        {
            var itemCfg = ("" + reader.ReadLine()).Trim().Replace(" ", "").ToLower(); //为了兼容性才这样写
            if (StartsWithoutComment(itemCfg))
            {
                if (itemCfg.Contains("="))
                {
                    if (itemCfg.Contains("usedefaultntpserver=false"))
                    {
                        useDefaultNtpServer = false;
                    }
                    else if (itemCfg.Contains("autosynctime=true"))
                    {
                        autoSyncTime = true;
                    }
                    else if (itemCfg.Contains("verbose=true"))
                    {
                        verbose = true;
                    }
                    else if (itemCfg.Contains("timeout="))
                    {
                        int.TryParse(itemCfg.Substring(itemCfg.IndexOf('=') + 1), out timeout);
                    }
                }
                else if (!ntpServers.Contains(itemCfg))
                {
                    ntpServers.Add(itemCfg);
                }
            }
        }
        if (!useDefaultNtpServer && ntpServers.Count > 3)
        {
            ntpServers.RemoveRange(0, 3);
        }
        using (cts = new CancellationTokenSource())
        {
            ntpServers.ForEach(it => ThreadPool.QueueUserWorkItem(GetNtpTime, it));
            if (!evt.WaitOne(timeout <= 0 ? 30000 : timeout))
            {
                Environment.FailFast(asm.Location + " 同步时间失败，网络不畅通。");
            }
            if (args != null)
            {
                Thread.Sleep(3000);
                Stop();
            }
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
        return (((x >> 24) & 0x000000ff) | ((x >> 8) & 0x0000ff00) | ((x << 8) & 0x00ff0000) | ((x << 24) & 0xff000000)) * 1000;
    }

    private void GetNtpTime(object serv)
    {
        var time = new DateTime(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        byte[] data = new byte[48];
        data[0] = 0x1B;
        IPAddress[] ip = null;
        while (true)
        {
            try
            {
                ip = Dns.GetHostAddresses((string)serv);
                using Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.SendTimeout = socket.ReceiveTimeout = 5000;
                socket.Connect(ip, 123);
                socket.Send(data);
                socket.Receive(data);
                time = time.AddMilliseconds((SwapEndianness(data, 44) >> 32) + SwapEndianness(data, 40));
            }
            catch
            {
                if (ip == null && !cts.IsCancellationRequested)
                {
                    Thread.Sleep(1000);
                    continue;
                }
                else
                {
                    Thread.CurrentThread.Abort();
                }
            }
            break;
        }
        if (!cts.IsCancellationRequested)
        {
            cts.Cancel();
            SetSystemTime(time, serv);
            evt.Set();
        }
    }

    private void SetSystemTime(DateTime time, object serv)
    {
        st.wYear = (short)time.Year;
        st.wMonth = (short)time.Month;
        st.wDay = (short)time.Day;
        st.wHour = (short)time.Hour;
        st.wMinute = (short)time.Minute;
        st.wSecond = (short)time.Second;
        if (autoSyncTime)
        {
            SetSystemTime(ref st);
        }
        if (verbose)
        {
            var msg = string.Format("/c echo Get Datetime from \"{0}\", {1} && pause", serv, time.ToLocalTime());
            try
            {
                System.Diagnostics.Process.Start("cmd.exe", msg);
            }
            catch
            {
                Environment.FailFast(asm.Location + " 无法输出详细信息，请检查此电脑的 PATH 环境变量。");
            }
        }
    }

    protected override void OnStart(string[] args)
    {
        ThreadPool.QueueUserWorkItem(LoadProfile, args);
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