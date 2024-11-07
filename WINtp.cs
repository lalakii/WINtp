using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

[assembly: AssemblyProduct("WINtp")]
[assembly: AssemblyVersion("1.5.0.0")]
[assembly: AssemblyFileVersion("1.5.0.0")]
[assembly: AssemblyTitle("A Simple NTP Client")]
[assembly: AssemblyCopyright("Copyright (C) 2024 lalaki.cn")]

[System.ComponentModel.DesignerCategory("")]
public class WINtp : System.ServiceProcess.ServiceBase
{
    private const int NTP_TYPE = 0;
    private const int HTTP_TYPE = 1;
    private const int WINTP_TIMEDOUT_ERROR = 1;
    private const int PROCESS_START_ERROR = 2;
    private const int NTP_CONNECTION_ERROR = 3;
    private bool autoSyncTime = false;
    private static SystemTime st;
    private bool verbose = false;
    private bool useSSL = false;
    private Timer timer;
    private int delay;
    private static readonly Assembly asm = typeof(WINtp).Assembly;
    private static readonly char[] comments = ['-', '#', '/', ';', '<', '=', ':'];
    private static readonly DateTime timeOf1900 = new(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

    public static void Main(string[] args)
    {
        using var ntp = new WINtp();
        if (args != null && new string[] { "-k", "/k" }.Contains(args.LastOrDefault(), StringComparer.OrdinalIgnoreCase))
        {
            Run(ntp);
        }
        else
        {
            ntp.LoadProfile(null);
        }
    }

    private void LoadProfile(object args)
    {
        var timeout = 0;
        var cfgPath = Path.Combine(Path.GetDirectoryName(asm.Location), "ntp.ini");
        List<TimeServer> servers = [];
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
            if (StartsWithoutComment(itemCfg) && itemCfg.Contains("="))
            {
                if (itemCfg.Contains("autosynctime=true"))
                {
                    autoSyncTime = true;
                }
                else if (itemCfg.Contains("verbose=true"))
                {
                    verbose = true;
                }
                else if (itemCfg.Contains("usessl=true"))
                {
                    useSSL = true;
                }
                else if (itemCfg.Contains("timeout="))
                {
                    int.TryParse(itemCfg.Substring(itemCfg.IndexOf('=') + 1), out timeout);
                }
                else if (itemCfg.Contains("delay="))
                {
                    int.TryParse(itemCfg.Substring(itemCfg.IndexOf('=') + 1), out delay);
                }
                else
                {
                    var isNtp = itemCfg.Contains("ntps=");
                    if (isNtp || itemCfg.Contains("urls="))
                    {
                        var strArr = (itemCfg.Substring(itemCfg.IndexOf('=') + 1) + "").Trim().Split(';');
                        foreach (var it in strArr)
                        {
                            var mHost = it.Trim();
                            if (mHost != "")
                            {
                                var serv = new TimeServer
                                {
                                    host = mHost,
                                    type = isNtp ? NTP_TYPE : HTTP_TYPE
                                };
                                if (!servers.Contains(serv))
                                {
                                    servers.Add(serv);
                                }
                            }
                        }
                    }
                }
            }
        }
        if (servers.Count != 0)
        {
            ManualResetEvent evt = new(false);
            using CancellationTokenSource cts = new();
            servers.ForEach(it =>
            {
                it.cts = cts;
                it.evt = evt;
                ThreadPool.QueueUserWorkItem(GetNetTime, it);
            });
            bool hasTimedOut = !evt.WaitOne(timeout <= 0 ? 30000 : timeout);
            evt.Close();
            if (hasTimedOut)
            {
                Environment.FailFast(GetFailureMessage(WINTP_TIMEDOUT_ERROR));
            }
            if (args != null && timer == null)
            {
                if (delay <= 0)
                {
                    Thread.Sleep(3000);
                    Stop();
                }
                else
                {
                    timer = new Timer(_ =>
                   {
                       OnStart(null);
                   }, null, 0, delay * 1000);
                }
            }
        }
    }

    protected override void OnStop()
    {
        timer?.Dispose();
    }

    private static bool StartsWithoutComment(string str)
    {
        return str != "" && !comments.Contains(str.First());
    }

    private void GetNetTime(object obj)
    {
        var serv = (TimeServer)obj;
        IPAddress[] ip = null;
        DateTime time = DateTime.MinValue;
        while (!serv.cts.IsCancellationRequested)
        {
            try
            {
                ip = Dns.GetHostAddresses(serv.host);
                time = serv.type == HTTP_TYPE ? GetHttpTime(ip, serv.host) : GetNtpTime(ip);
            }
            catch
            {
                if (!serv.cts.IsCancellationRequested && ip == null)
                {
                    Thread.Sleep(1000);
                    continue;
                }
                else
                {
                    if (verbose)
                    {
                        using var log = new EventLog("Application", ".", string.Format("{0} cannot connect to \"{1}\"", asm.GetName().Name, serv));
                        log.WriteEntry(GetFailureMessage(NTP_CONNECTION_ERROR), EventLogEntryType.Error);
                    }
                    Thread.CurrentThread.Abort();
                }
            }
            break;
        }
        if (!DateTime.MinValue.Equals(time) && !serv.cts.IsCancellationRequested)
        {
            serv.cts.Cancel();
            if (autoSyncTime)
            {
                Win32SetSystemTime(time);
            }
            if (verbose)
            {
                var msg = string.Format("/c echo {0} Get datetime from \"{1}\", AutoSyncTime: {2} && pause", time.ToLocalTime(), serv.host, autoSyncTime);
                try
                {
                    Process.Start("cmd.exe", msg);
                }
                catch
                {
                    Environment.FailFast(GetFailureMessage(PROCESS_START_ERROR));
                }
            }
            serv.evt.Set();
        }
    }

    private DateTime GetHttpTime(IPAddress[] ip, string host)
    {
        using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        client.Connect(ip, useSSL ? 443 : 80);
        client.Send(Encoding.ASCII.GetBytes(string.Format("HEAD / HTTP/1.1\r\nHost: {0}\r\nConnection: close\r\n\r\n", host)));
        var reader = new StreamReader(new NetworkStream(client));
        while (reader.Peek() != -1)
        {
            var line = reader.ReadLine();
            if (line.StartsWith("Date:", StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToDateTime(line.Substring(5)).ToUniversalTime();//rfc1123
            }
        }
        throw new SocketException();
    }

    private static DateTime GetNtpTime(IPAddress[] ip)
    {
        var data = new byte[48];
        data[0] = 0x1B;
        using Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Connect(ip, 123);
        socket.Send(data);
        socket.Receive(data);
        unsafe
        {
            fixed (byte* ptr = data)
            {
                var intPart = (ulong*)(ptr + 40);
                var fractPart = (ulong*)(ptr + 44);
                return timeOf1900.AddMilliseconds((SwapEndianness(*fractPart) >> 32) + SwapEndianness(*intPart));
            }
        }
    }

    private static string GetFailureMessage(int errorCode)
    {
        return string.Format("程序路径: {0}\r\n代码: {1}, 可在 https://wintp.sourceforge.io/ 获取帮助。", asm.Location, errorCode);
    }

    private static void Win32SetSystemTime(DateTime time)
    {
        st.wYear = (short)time.Year;
        st.wMonth = (short)time.Month;
        st.wDay = (short)time.Day;
        st.wHour = (short)time.Hour;
        st.wMinute = (short)time.Minute;
        st.wSecond = (short)time.Second;
        SetSystemTime(ref st);
    }

    protected override void OnStart(string[] args)
    {
        ThreadPool.QueueUserWorkItem(LoadProfile, args);
    }

    // stackoverflow.com/a/3294698/162671
    private static ulong SwapEndianness(ulong x)
    {
        return (((x >> 24) & 0x000000ff) | ((x >> 8) & 0x0000ff00) | ((x << 8) & 0x00ff0000) | ((x << 24) & 0xff000000)) * 1000;
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

    private struct TimeServer
    {
        public int type;
        public string host;
        public ManualResetEvent evt;
        public CancellationTokenSource cts;
    }
}