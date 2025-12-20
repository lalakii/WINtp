using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyProduct("WINtp")]
[assembly: AssemblyVersion("1.9.0.0")]
[assembly: AssemblyFileVersion("1.9.0.0")]
[assembly: AssemblyTitle("A Simple NTP Client")]
[assembly: AssemblyCopyright("Copyright (C) 2026 lalaki.cn")]

[System.ComponentModel.DesignerCategory("")]
public class WINtp : System.ServiceProcess.ServiceBase
{
    private const int HttpRequestType = 1;
    private const int NetworkConnectionError = -3;
    private const int NtpRequestType = 0;
    private const int ProcessStartFailure = -2;
    private const int TimeoutError = -1;
    private static readonly string[] PArray = ["-k", "/k"];
    private static readonly DateTime TimeOf1900 = new(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Assembly Wintp = typeof(WINtp).Assembly;
    private static SystemTime st;
    private bool autoSync;
    private int delay;
    private Timer? timer;
    private bool useSSL;
    private bool verbose;

    public static void Main(string[] args)
    {
        using WINtp ntp = new();
        if (args != null && PArray.Contains(args.LastOrDefault(), StringComparer.OrdinalIgnoreCase))
        {
            Run(ntp);
        }
        else
        {
            ntp.LoadProfile(null);
        }
    }

    protected override void OnStart(string[]? args)
    {
        ThreadPool.UnsafeQueueUserWorkItem(this.LoadProfile, args);
    }

    protected override void OnStop()
    {
        this.timer?.Dispose();
    }

    private static void AddAll(List<TimeServer> servers, string[] items, bool isNtp)
    {
        foreach (var it in items)
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

    private static string GetFailureMessage(int errorCode)
    {
        return string.Format("程序路径: {0}\r\n代码: {1}, 可在 https://wintp.sourceforge.io/ 获取帮助。", Wintp.Location, errorCode);
    }

    // stackoverflow.com/a/20847931/28134812
    [DllImport("kernel32.dll")]
    private static extern bool SetSystemTime(ref SystemTime st);

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

    private DateTime GetNtpTime(string ntpServerUrl)
    {
        var data = new byte[48];
        data[0] = 0x1B;
        using Socket udp = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
        {
            SendTimeout = 5000,
            ReceiveTimeout = 5000,
        };
        udp.SendTo(data, new IPEndPoint(Dns.GetHostAddresses(ntpServerUrl).FirstOrDefault(), 123));
        udp.Receive(data);
        long intPart = BitConverter.ToUInt32(data, 40);
        long fractPart = BitConverter.ToUInt32(data, 44);
        intPart = (uint)(IPAddress.NetworkToHostOrder(intPart) >> 32);
        fractPart = (uint)(IPAddress.NetworkToHostOrder(fractPart) >> 32);
        long milliseconds = (intPart * 1000) + ((fractPart * 1000) >> 32);
        var ntpDate = TimeOf1900.AddMilliseconds(milliseconds).ToUniversalTime();
        Console.WriteLine("Time: {0}, NTP Server: {1}", ntpDate, ntpServerUrl);
        return ntpDate;
    }

    private DateTime GetHttpTime(string httpUrl)
    {
        if (!httpUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            httpUrl = $"{(useSSL ? "https://" : "http://")}{httpUrl}";
        }

        var req = WebRequest.Create(httpUrl);
        req.Timeout = 5000;
        req.Method = "HEAD";
        using var httpResposne = (HttpWebResponse)req.GetResponse();
        var httpDate = Convert.ToDateTime(httpResposne.Headers["Date"]).ToUniversalTime();
        Console.WriteLine("Time: {0}, HTTP Server: {1}", httpDate, httpUrl);
        return httpDate;
    }

    private void GetNetTime(object obj)
    {
        if (obj is TimeServer serv)
        {
            var sh = serv.ResetEvent.SafeWaitHandle;
            DateTime? time = null;
            while (!sh.IsClosed)
            {
                try
                {
                    time = serv.RequestType == NtpRequestType ? this.GetNtpTime(serv.HostName) : this.GetHttpTime(serv.HostName);
                }
                catch
                {
                    if (this.verbose)
                    {
                        using EventLog log = new("Application", ".", string.Format("{0} cannot connect to \"{1}\"", Wintp.GetName().Name, serv));
                        log.WriteEntry(GetFailureMessage(NetworkConnectionError), EventLogEntryType.Error);
                    }

                    if (!sh.IsClosed)
                    {
                        Thread.Sleep(1000);
                        continue;
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

                Console.WriteLine("set OK");
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

    private void LoadProfile(object? args)
    {
        List<TimeServer> servers = [];
        var cfgPath = Path.Combine(Path.GetDirectoryName(Wintp.Location), "WINtp.exe.config");
        if (!File.Exists(cfgPath))
        {
            using var pCfg = File.Create(cfgPath);
            Wintp.GetManifestResourceStream(Wintp.GetManifestResourceNames().FirstOrDefault()).CopyTo(pCfg);
        }

        var reader = new System.Configuration.AppSettingsReader();
        _ = bool.TryParse($"{reader.GetValue("Verbose", typeof(bool))}", out verbose);
        _ = bool.TryParse($"{reader.GetValue("AutoSyncTime", typeof(bool))}", out autoSync);
        _ = bool.TryParse($"{reader.GetValue("UseSsl", typeof(bool))}", out useSSL);
        _ = int.TryParse($"{reader.GetValue("Delay", typeof(int))}", out delay);
        _ = int.TryParse($"{reader.GetValue("Timeout", typeof(int))}", out int timeout);
        AddAll(servers, $"{reader.GetValue("Ntps", typeof(string))}".Split(';'), true);
        AddAll(servers, $"{reader.GetValue("Urls", typeof(string))}".Split(';'), false);
        if (servers.Count != 0)
        {
            ManualResetEvent evt = new(false);
            servers.ForEach(it =>
            {
                it.ResetEvent = evt;
                ThreadPool.UnsafeQueueUserWorkItem(this.GetNetTime, it);
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

        Thread.Sleep(1000);
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