using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

[assembly: AssemblyProduct("WINtp")]
[assembly: AssemblyVersion("1.7.0.0")]
[assembly: AssemblyFileVersion("1.7.0.0")]
[assembly: AssemblyTitle("A Simple NTP Client")]
[assembly: AssemblyCopyright("Copyright (C) 2024 lalaki.cn")]

[System.ComponentModel.DesignerCategory("")]
public class WINtp : System.ServiceProcess.ServiceBase
{
    private const string Comments = "'-#/;<=:";
    private const int HttpRequestType = 1;
    private const int NetworkConnectionError = -3;
    private const int NtpRequestType = 0;
    private const int ProcessStartFailure = -2;
    private const int TimeoutError = -1;
    private static readonly DateTime TimeOf1900 = new(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Assembly Wintp = typeof(WINtp).Assembly;
    private static SystemTime st;
    private bool autoSync;
    private int delay;
    private Timer timer;
    private bool useSSL;
    private bool verbose;

    public static void Main(string[] args)
    {
        using WINtp ntp = new();
        if (args != null && new string[] { "-k", "/k" }.Contains(args.LastOrDefault(), StringComparer.OrdinalIgnoreCase))
        {
            Run(ntp);
        }
        else
        {
            ntp.LoadProfile(null);
        }
    }

    protected override void OnStart(string[] args)
    {
        ThreadPool.QueueUserWorkItem(this.LoadProfile, args);
    }

    protected override void OnStop()
    {
        this.timer?.Dispose();
    }

    private static string GetFailureMessage(int errorCode)
    {
        return string.Format("程序路径: {0}\r\n代码: {1}, 可在 https://wintp.sourceforge.io/ 获取帮助。", Wintp.Location, errorCode);
    }

    private static DateTime GetNtpTime(IPAddress[] ip)
    {
        var data = new byte[48];
        data[0] = 0x1B;
        using Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.SendTo(data, new IPEndPoint(ip.FirstOrDefault(), 123));
        socket.Receive(data);
        unsafe
        {
            fixed (byte* ptr = data)
            {
                var intPart = (ulong*)(ptr + 40);
                var fractPart = (ulong*)(ptr + 44);
                return TimeOf1900.AddMilliseconds((SwapEndianness(*fractPart) >> 32) + SwapEndianness(*intPart));
            }
        }
    }

    private static bool IsComments(string str)
    {
        return str != string.Empty && !Comments.Contains(str.FirstOrDefault());
    }

    // stackoverflow.com/a/20847931/28134812
    [DllImport("kernel32.dll")]
    private static extern bool SetSystemTime(ref SystemTime st);

    // stackoverflow.com/a/3294698/162671
    private static ulong SwapEndianness(ulong x)
    {
        return (((x >> 24) & 0x000000ff) | ((x >> 8) & 0x0000ff00) | ((x << 8) & 0x00ff0000) | ((x << 24) & 0xff000000)) * 1000UL;
    }

    private static void Win32SetSystemTime(DateTime t)
    {
        st.Year = (short)t.Year;
        st.Month = (short)t.Month;
        st.Day = (short)t.Day;
        st.Hour = (short)t.Hour;
        st.Minute = (short)t.Minute;
        st.Second = (short)t.Second;
        st.Millisecond = (short)t.Millisecond;
        SetSystemTime(ref st);
    }

    private DateTime GetHttpTime(IPAddress[] ip, string host)
    {
        using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Connect(ip, this.useSSL ? 443 : 80);
        socket.Send(System.Text.Encoding.UTF8.GetBytes(string.Format("HEAD / HTTP/1.1\r\nHost: {0}\r\nConnection: close\r\n\r\n", host)));
        StreamReader rdr = new(new NetworkStream(socket));
        while (rdr.Peek() != -1)
        {
            var line = rdr.ReadLine();
            if (line.StartsWith("Date:", StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToDateTime(line.Substring(5)).ToUniversalTime();
            }
        }

        throw new SocketException();
    }

    private void GetNetTime(object obj)
    {
        if (obj is TimeServer serv)
        {
            var sh = serv.ResetEvent.SafeWaitHandle;
            IPAddress[] ip = null;
            DateTime? time = null;
            while (!sh.IsClosed)
            {
                try
                {
                    ip = Dns.GetHostAddresses(serv.HostName);
                    time = serv.RequestType == NtpRequestType ? GetNtpTime(ip) : this.GetHttpTime(ip, serv.HostName);
                }
                catch
                {
                    if (!sh.IsClosed && ip == null)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }
                    else if (this.verbose)
                    {
                        using EventLog log = new("Application", ".", string.Format("{0} cannot connect to \"{1}\"", Wintp.GetName().Name, serv));
                        log.WriteEntry(GetFailureMessage(NetworkConnectionError), EventLogEntryType.Error);
                    }

                    Thread.CurrentThread.Abort();
                }

                break;
            }

            if (!sh.IsClosed)
            {
                serv.ResetEvent.Set();
            }

            if (time is DateTime local && !sh.IsClosed)
            {
                serv.ResetEvent.Close();
                if (this.autoSync)
                {
                    Win32SetSystemTime(local);
                }

                if (this.verbose)
                {
                    var msg = string.Format("/c echo {0} Get datetime from \"{1}\", AutoSyncTime: {2} && pause", local.ToLocalTime(), serv.HostName, this.autoSync);
                    try
                    {
                        Process.Start("cmd.exe", msg);
                    }
                    catch
                    {
                        Environment.FailFast(GetFailureMessage(ProcessStartFailure));
                    }
                }
            }
        }
    }

    private void LoadProfile(object args)
    {
        var timeout = 0;
        List<TimeServer> servers = [];
        var cfgPath = Path.Combine(Path.GetDirectoryName(Wintp.Location), "ntp.ini");
        var hasCfg = File.Exists(cfgPath);
        if (hasCfg)
        {
            File.SetAttributes(cfgPath, File.GetAttributes(cfgPath) & ~FileAttributes.ReadOnly);
        }

        using (FileStream pCfg = new(cfgPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
        {
            if (!hasCfg)
            {
                Wintp.GetManifestResourceStream(Wintp.GetManifestResourceNames().FirstOrDefault()).CopyTo(pCfg);
                pCfg.Position = 0;
            }

            StreamReader rdr = new(pCfg);
            while (rdr.Peek() != -1)
            {
                var itemCfg = (string.Empty + rdr.ReadLine()).Trim().Replace(" ", string.Empty).ToLower();
                if (IsComments(itemCfg) && itemCfg.Contains("="))
                {
                    if (itemCfg.Contains("autosynctime=true"))
                    {
                        this.autoSync = true;
                    }
                    else if (itemCfg.Contains("verbose=true"))
                    {
                        this.verbose = true;
                    }
                    else if (itemCfg.Contains("usessl=true"))
                    {
                        this.useSSL = true;
                    }
                    else if (itemCfg.Contains("timeout="))
                    {
                        int.TryParse(itemCfg.Substring(itemCfg.IndexOf('=') + 1), out timeout);
                    }
                    else if (itemCfg.Contains("delay="))
                    {
                        int.TryParse(itemCfg.Substring(itemCfg.IndexOf('=') + 1), out this.delay);
                    }
                    else
                    {
                        var isNtp = itemCfg.Contains("ntps=");
                        if (isNtp || itemCfg.Contains("urls="))
                        {
                            foreach (var it in (itemCfg.Substring(itemCfg.IndexOf('=') + 1) + string.Empty).Trim().Split(';'))
                            {
                                var mHost = it.Trim();
                                if (mHost != string.Empty)
                                {
                                    TimeServer serv = new()
                                    {
                                        HostName = mHost,
                                        RequestType = isNtp ? NtpRequestType : HttpRequestType,
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
        }

        if (servers.Count != 0)
        {
            ManualResetEvent evt = new(false);
            servers.ForEach(it =>
            {
                it.ResetEvent = evt;
                ThreadPool.QueueUserWorkItem(this.GetNetTime, it);
            });
            if (!evt.WaitOne(timeout < 1 ? 30000 : timeout))
            {
                Environment.FailFast(GetFailureMessage(TimeoutError));
            }

            if (args != null && this.timer == null)
            {
                if (this.delay < 1)
                {
                    Thread.Sleep(3000);
                    this.Stop();
                }
                else
                {
                    this.timer = new(_ => this.OnStart(null), null, 0, this.delay * 1000);
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemTime
    {
        public short Year;
        public short Month;
        public short DayOfWeek;
        public short Day;
        public short Hour;
        public short Minute;
        public short Second;
        public short Millisecond;
    }

    private struct TimeServer
    {
        public string HostName;
        public int RequestType;
        public ManualResetEvent ResetEvent;
    }
}