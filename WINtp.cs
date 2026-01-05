using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;

[assembly: AssemblyProduct("WINtp")]
[assembly: AssemblyVersion("2.1.0.0")]
[assembly: AssemblyFileVersion("2.1.0.0")]
[assembly: AssemblyTitle("A Simple NTP Client")]
[assembly: AssemblyCopyright("Copyright (C) 2026 lalaki.cn")]

[System.ComponentModel.DesignerCategory("")]
public class WINtp : System.ServiceProcess.ServiceBase
{
    public const int NtpRequestType = 0;
    public const int HttpRequestType = 1;
    private const int NetworkConnectionError = -3;
    private const int TimeoutError = -1;
    private static readonly string[] ParamsArray = ["-k", "/k"];
    private static readonly string[] NormalParamsArray = ["-d", "/d"];
    private static readonly DateTime TimeOf1900 = new(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Assembly Wintp = typeof(WINtp).Assembly;
    private static SystemTime st;
    private bool autoSync;
    private int delay;
    private int netTimeout;
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
        else if (args != null && NormalParamsArray.Contains(args.LastOrDefault(), StringComparer.OrdinalIgnoreCase))
        {
            ntp.LoadProfileOrGetNetworkTime(args);
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

    private static void ShowWindow(List<TimeSynchronizationOptions> configs)
    {
        Thread.CurrentThread.SetApartmentState(ApartmentState.Unknown);
        Thread.CurrentThread.SetApartmentState(ApartmentState.STA);
        new Application().Run(new WINtpMainWindow(configs));
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
            SendTimeout = this.netTimeout,
            ReceiveTimeout = this.netTimeout,
        };
        udp.SendTo(data, new IPEndPoint(Dns.GetHostAddresses(ntpServerUrl).FirstOrDefault(), 123));
        udp.Receive(data);
        long intPart = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(data, 40)) & 0xFFFFFFFFL;
        long fractPart = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(data, 44)) & 0xFFFFFFFFL;
        long milliseconds = (intPart * 1000) + ((fractPart * 1000) >> 32);
        var ntpDate = TimeOf1900.AddMilliseconds(milliseconds).ToUniversalTime();
        Console.WriteLine("Time: {0}, NTP Server: {1}", ntpDate, ntpServerUrl);
        return ntpDate;
    }

    private void LogPrintln(string errorMsg, int errorCode)
    {
        if (this.verbose)
        {
            try
            {
                using EventLog log = new("Application", ".", errorMsg);
                log.WriteEntry(string.Format("程序路径: {0}\r\n代码: {1}, 可在 https://wintp.sourceforge.io/ 获取帮助。", Wintp.Location, errorCode), EventLogEntryType.Error);
            }
            catch
            {
            }
        }
    }

    private DateTime GetHttpTime(string httpUrl)
    {
        if (!httpUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            httpUrl = $"{(useSSL ? "https://" : "http://")}{httpUrl}";
        }

        var req = (HttpWebRequest)WebRequest.Create(httpUrl);
        req.Method = "HEAD";
        req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36 Edg/143.0.0.0";
        req.Timeout = this.netTimeout;
        req.ReadWriteTimeout = this.netTimeout;
        req.ContinueTimeout = this.netTimeout;
        using var httpResposne = (HttpWebResponse)req.GetResponse();
        var httpDate = Convert.ToDateTime(httpResposne.Headers["Date"]).ToUniversalTime();
        Console.WriteLine("Time: {0}, HTTP Server: {1}", httpDate, httpUrl);
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
                        time = config.RequestType == NtpRequestType ? this.GetNtpTime(serverUrl) : this.GetHttpTime(serverUrl);
                    }
                    catch
                    {
                        LogPrintln(string.Format("Unable to connect to the server({0}). Failed to sync network time.", serverUrl), NetworkConnectionError);
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
                foreach (var it in Wintp.GetManifestResourceNames())
                {
                    if (it.EndsWith("WINtp.exe.config", StringComparison.OrdinalIgnoreCase))
                    {
                        Wintp.GetManifestResourceStream(it).CopyTo(pCfg);
                        break;
                    }
                }
            }

            System.Configuration.AppSettingsReader reader = new();
            int timeout = 30000;
            try
            {
                _ = bool.TryParse($"{reader.GetValue("Verbose", typeof(bool))}", out verbose);
                _ = bool.TryParse($"{reader.GetValue("AutoSyncTime", typeof(bool))}", out autoSync);
                _ = bool.TryParse($"{reader.GetValue("UseSsl", typeof(bool))}", out useSSL);
                _ = int.TryParse($"{reader.GetValue("Delay", typeof(int))}", out delay);
                _ = int.TryParse($"{reader.GetValue("Timeout", typeof(int))}", out timeout);
                _ = int.TryParse($"{reader.GetValue("NetworkTimeout", typeof(int))}", out netTimeout);
                AddAll(configs, $"{reader.GetValue("Ntps", typeof(string))}".Split(';'), true);
                AddAll(configs, $"{reader.GetValue("Urls", typeof(string))}".Split(';'), false);
            }
            catch
            {
                File.Delete(cfgPath);
                try
                {
                    Process.Start(Wintp.Location);
                }
                catch
                {
                }
                finally
                {
                    Environment.FailFast(string.Empty);
                }
            }

            if (verbose)
            {
                Console.WriteLine("Verbose: {0}", verbose);
                Console.WriteLine("AutoSyncTime: {0}", autoSync);
                Console.WriteLine("UseSsl: {0}", useSSL);
                Console.WriteLine("Delay: {0}", delay);
                Console.WriteLine("Timeout: {0}", timeout);
                Console.WriteLine("NetworkTimeout: {0}", netTimeout);
                foreach (var item in configs)
                {
                    Console.WriteLine("Server: {0}, Type: {1}", item.HostName, item.RequestType);
                }
            }

            if (configs.Count != 0)
            {
                ManualResetEvent evt = new(false);
                foreach (var it in configs)
                {
                    it.ResetEvent = evt;
                    ThreadPool.UnsafeQueueUserWorkItem(this.LoadProfileOrGetNetworkTime, it);
                }

                if (args == null && this.ServiceHandle == IntPtr.Zero)
                {
                    ShowWindow(configs);
                }

                if (!evt.SafeWaitHandle.IsClosed && !evt.WaitOne(timeout < 1 ? 30000 : timeout))
                {
                    LogPrintln("Timeout occurred while fetching network time. Please check the logs for details.", TimeoutError);
                    Environment.FailFast(string.Empty);
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
            else
            {
                ShowWindow(configs);
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

    public class TimeSynchronizationOptions
    {
        public string? HostName { get; set; }

        public int RequestType { get; set; }

        public ManualResetEvent? ResetEvent { get; set; }
    }
}