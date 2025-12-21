using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyProduct("WINtp")]
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]
[assembly: AssemblyTitle("A Simple NTP Client")]
[assembly: AssemblyCopyright("Copyright (C) 2026 lalaki.cn")]

[System.ComponentModel.DesignerCategory("")]
public class WINtp : System.ServiceProcess.ServiceBase
{
    private const int HttpRequestType = 1;
    private const int NetworkConnectionError = -3;
    private const int NtpRequestType = 0;
    private const int TimeoutError = -1;
    private static readonly string[] ParamsArray = ["-k", "/k"];
    private static readonly DateTime TimeOf1900 = new(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Assembly Wintp = typeof(WINtp).Assembly;
    private static SystemTime st;
    private bool autoSync;
    private int delay;
    private Timer? timer;
    private bool useSSL;
    private bool verbose;

    public static void Main(string[]? args)
    {
        using WINtp ntp = new();
        if (args != null && ParamsArray.Contains(args.LastOrDefault(), StringComparer.OrdinalIgnoreCase))
        {
            Run(ntp);
        }
        else
        {
            ntp.LoadProfileOrGetNetworkTime(null);
        }
    }

    protected override void OnStart(string[]? args)
    {
        base.OnStart(args);
        ThreadPool.UnsafeQueueUserWorkItem(this.LoadProfileOrGetNetworkTime, args);
    }

    protected override void OnStop()
    {
        this.timer?.Dispose();
        base.OnStop();
    }

    private static void AddAll(List<TimeSynchronizationOptions> configs, string[] items, bool isNtp)
    {
        foreach (var it in items)
        {
            var mHost = it.Trim();
            if (mHost != string.Empty)
            {
                TimeSynchronizationOptions config = new()
                {
                    HostName = mHost,
                    RequestType = isNtp ? NtpRequestType : HttpRequestType,
                };
                if (!configs.Contains(config))
                {
                    configs.Add(config);
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

    private static DateTime GetNtpTime(string ntpServerUrl)
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
        Debug.WriteLine("Time: {0}, NTP Server: {1}", ntpDate, ntpServerUrl);
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
        Debug.WriteLine("Time: {0}, HTTP Server: {1}", httpDate, httpUrl);
        return httpDate;
    }

    private void LoadProfileOrGetNetworkTime(object? args)
    {
        if (args is TimeSynchronizationOptions config)
        {
            var evt = config.ResetEvent;
            var serverUrl = config.HostName;
            if (evt != null && serverUrl != null)
            {
                var sh = evt.SafeWaitHandle;
                DateTime? time = null;
                while (!sh.IsClosed)
                {
                    try
                    {
                        time = config.RequestType == NtpRequestType ? GetNtpTime(serverUrl) : this.GetHttpTime(serverUrl);
                    }
                    catch
                    {
                        if (this.verbose)
                        {
                            using EventLog log = new("Application", ".", string.Format("{0} cannot connect to \"{1}\"", Wintp.GetName().Name, serverUrl));
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
                    evt.Set();
                }

                if (!sh.IsClosed && time is DateTime local)
                {
                    evt.Close();
                    if (this.autoSync)
                    {
                        Win32SetSystemTime(local);
                    }
                }
            }
        }
        else
        {
            List<TimeSynchronizationOptions> configs = [];
            var cfgPath = Path.Combine(Path.GetDirectoryName(Wintp.Location), "WINtp.exe.config");
            if (!File.Exists(cfgPath))
            {
                using var pCfg = File.Create(cfgPath);
                Wintp.GetManifestResourceStream(Wintp.GetManifestResourceNames().FirstOrDefault()).CopyTo(pCfg);
            }

            System.Configuration.AppSettingsReader reader = new();
            _ = bool.TryParse($"{reader.GetValue("Verbose", typeof(bool))}", out verbose);
            _ = bool.TryParse($"{reader.GetValue("AutoSyncTime", typeof(bool))}", out autoSync);
            _ = bool.TryParse($"{reader.GetValue("UseSsl", typeof(bool))}", out useSSL);
            _ = int.TryParse($"{reader.GetValue("Delay", typeof(int))}", out delay);
            _ = int.TryParse($"{reader.GetValue("Timeout", typeof(int))}", out int timeout);
            AddAll(configs, $"{reader.GetValue("Ntps", typeof(string))}".Split(';'), true);
            AddAll(configs, $"{reader.GetValue("Urls", typeof(string))}".Split(';'), false);
            if (configs.Count != 0)
            {
                ManualResetEvent evt = new(false);
                foreach (var it in configs)
                {
                    it.ResetEvent = evt;
                    ThreadPool.UnsafeQueueUserWorkItem(this.LoadProfileOrGetNetworkTime, it);
                }

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

                Thread.Sleep(3000);
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

    private class TimeSynchronizationOptions
    {
        public string? HostName { get; set; }

        public int RequestType { get; set; }

        public ManualResetEvent? ResetEvent { get; set; }
    }
}