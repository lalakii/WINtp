using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows;

[assembly: AssemblyProduct("WINtp")]
[assembly: AssemblyVersion("2.4.0.0")]
[assembly: AssemblyFileVersion("2.4.0.0")]
[assembly: AssemblyTitle("A Simple NTP Client")]
[assembly: AssemblyCopyright("Copyright (C) 2026 lalaki.cn")]

[System.ComponentModel.DesignerCategory("")]
public class WINtp : System.ServiceProcess.ServiceBase
{
    public const int ProgressiveDelay = 800;
    public const int NtpRequestType = 0;
    public const int HttpRequestType = 1;
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
#pragma warning disable SA1401 // Fields should be private
    public WINtpServiceConfig mConfig;
#pragma warning restore SA1401 // Fields should be private
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
#pragma warning disable SA1310 // Field names should not contain underscore
    private const string SE_SYSTEMTIME_NAME = "SeSystemtimePrivilege";
    private const uint SE_PRIVILEGE_ENABLED = 0x00000002;
    private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    private const uint TOKEN_QUERY = 0x0008;
#pragma warning restore SA1310 // Field names should not contain underscore
    private const int NetworkConnectionError = -3;
    private const int TimeoutError = -1;
    private static readonly string[] ServiceParamsArray = ["-k", "/k"];
    private static readonly string[] NormalParamsArray = ["-d", "/d"];
    private static readonly DateTime TimeOf1900 = new(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Assembly Wintp = typeof(WINtp).Assembly;
    private static readonly string ConfigPath = $"{Wintp.Location}.json";
    private static SystemTime st;
    private Timer? timer;
    private int complete;
    private int netTimeout;

    public static void Main(string[]? args)
    {
        WINtpServiceConfig? pConfig = null;
        using (FileStream json = new(ConfigPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite))
        {
            DataContractJsonSerializer serializer = new(typeof(WINtpServiceConfig), new DataContractJsonSerializerSettings() { UseSimpleDictionaryFormat = true });
            if (json.Length > 0)
            {
                try
                {
                    if (serializer.ReadObject(json) is WINtpServiceConfig config)
                    {
                        pConfig = config;
                    }
                }
                catch
                {
                    Console.WriteLine("json serializer failed!");
                }
            }

            if (pConfig == null)
            {
                pConfig = new()
                {
                    SyncMode = 0,
                    Offset = 0,
                    Agreement = 0,
                    Timeout = 30000,
                    NetworkTimeout = 5000,
                    Delay = 3600,
                    UseSsl = false,
                    Verbose = false,
                    Hosts = new() { ["ntp.tencent.com"] = new(0, 0, true), ["ntp.aliyun.com"] = new(0, 0, true), ["time.cloudflare.com"] = new(0, 0, true), ["time.asia.apple.com"] = new(0, 0, true), ["rhel.pool.ntp.org"] = new(0, 0, true), ["www.baidu.com"] = new(1, 1, true), ["www.qq.com"] = new(1, 1, true), ["www.163.com"] = new(1, 1, true), },
                };
                json.SetLength(0);
                serializer.WriteObject(json, pConfig);
            }
        }

        using WINtp ntp = new() { mConfig = pConfig, netTimeout = (int)pConfig.NetworkTimeout };
        var userStarted = false;
        if (args is string[] argments && argments.Length > 0)
        {
            if (ServiceParamsArray.Contains(argments.LastOrDefault(), StringComparer.OrdinalIgnoreCase))
            {
                Run(ntp);
            }
            else if (NormalParamsArray.Contains(argments.LastOrDefault(), StringComparer.OrdinalIgnoreCase))
            {
                ntp.LoadProfileOrGetNetworkTime(argments);
            }
            else
            {
                userStarted = true;
            }
        }
        else
        {
            userStarted = true;
        }

        if (userStarted)
        {
            if (ntp.mConfig.Verbose && AllocConsole())
            {
                var asmName = Wintp.GetName();
                Console.Title = $"{asmName.Name} Logcat - Version: {asmName.Version}";
            }

            ntp.LoadProfileOrGetNetworkTime(null);
        }
    }

    public void SaveConfig()
    {
        DataContractJsonSerializer serializer = new(typeof(WINtpServiceConfig), new DataContractJsonSerializerSettings() { UseSimpleDictionaryFormat = true });
        var bakConfig = $"{ConfigPath}.bak";
        if (File.Exists(bakConfig))
        {
            File.Delete(bakConfig);
        }

        if (File.Exists(ConfigPath) && !File.Exists(bakConfig))
        {
            File.Move(ConfigPath, bakConfig);
        }

        using var json = File.Create(ConfigPath);
        serializer.WriteObject(json, mConfig);
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

    private static bool RequestPermission()
    {
        var permissionIsGranted = false;
        if (OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out IntPtr hToken) && LookupPrivilegeValue(null, SE_SYSTEMTIME_NAME, out LUID luid))
        {
            TOKEN_PRIVILEGES tp = new() { PrivilegeCount = 1, Privileges = [new() { Luid = luid, Attributes = SE_PRIVILEGE_ENABLED }] };
            if (AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero))
            {
                permissionIsGranted = true;
                Console.WriteLine($"PermissionIsGranted: {permissionIsGranted}");
            }

            if (hToken != IntPtr.Zero)
            {
                var status = CloseHandle(hToken);
                Console.WriteLine($"Token Handle Closed: {status}");
            }
        }

        return permissionIsGranted;
    }

    private static bool SetSystemTimeUnstable(DateTime t)
    {
        bool supported = true;
        if (RequestPermission() && GetSystemTimeAdjustmentPrecise(out ulong lpTimeAdjustment, out ulong lpTimeIncrement, out bool lpTimeAdjustmentDisabled))
        {
            ulong timeAdjustment;
            double tmpMs;
            while ((tmpMs = DateTime.Now.Subtract(t).TotalMilliseconds) < 0)
            {
                var ms = Math.Abs(tmpMs);
                if (ms < 3 * 1000)
                {
                    timeAdjustment = (ulong)(lpTimeIncrement / 1.5);
                }
                else
                {
                    timeAdjustment = lpTimeIncrement / ((ulong)(ms / 1000 * 0.95));
                }

                if (SetSystemTimeAdjustmentPrecise(timeAdjustment, false))
                {
                    Thread.Sleep(ProgressiveDelay);
                    if (SetSystemTimeAdjustmentPrecise(0, true))
                    {
                        t = t.AddMilliseconds(ProgressiveDelay);
                    }
                }
                else
                {
                    supported = false;
                    break;
                }
            }
        }
        else
        {
            supported = false;
        }

        return supported;
    }

    private static bool SetSystemTimeLegacy(DateTime t)
    {
        bool supported = true;
        if (RequestPermission() && GetSystemTimeAdjustment(out uint lpTimeAdjustment, out uint lpTimeIncrement, out bool lpTimeAdjustmentDisabled))
        {
            uint timeAdjustment;
            double tmpMs;
            while ((tmpMs = DateTime.Now.Subtract(t).TotalMilliseconds) < 0)
            {
                var ms = Math.Abs(tmpMs);
                if (ms < 3 * 1000)
                {
                    timeAdjustment = (uint)(lpTimeIncrement * 1.5);
                }
                else
                {
                    var maxTimeAdjustment = (ulong)(lpTimeIncrement * (ms / 1000 * 0.95));
                    if (maxTimeAdjustment > uint.MaxValue)
                    {
                        timeAdjustment = uint.MaxValue;
                    }
                    else
                    {
                        timeAdjustment = (uint)maxTimeAdjustment;
                    }
                }

                if (SetSystemTimeAdjustment(timeAdjustment, false))
                {
                    Thread.Sleep(ProgressiveDelay);
                    if (SetSystemTimeAdjustment(0, true))
                    {
                        t = t.AddMilliseconds(ProgressiveDelay);
                    }
                }
                else
                {
                    supported = false;
                    break;
                }
            }
        }
        else
        {
            supported = false;
        }

        return supported;
    }

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GetConsoleWindow();

    // stackoverflow.com/a/20847931/28134812
    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool SetSystemTime(ref SystemTime st);

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool GetSystemTimeAdjustment(out uint lpTimeAdjustment, out uint lpTimeIncrement, out bool lpTimeAdjustmentDisabled);

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool SetSystemTimeAdjustment(uint dwTimeAdjustment, bool bTimeAdjustmentDisabled);

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernelbase.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool GetSystemTimeAdjustmentPrecise(out ulong lpTimeAdjustment, out ulong lpTimeIncrement, out bool lpTimeAdjustmentDisabled);

    [DllImport("kernelbase.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool SetSystemTimeAdjustmentPrecise(ulong dwTimeAdjustment, bool bTimeAdjustmentDisabled);

    [DllImport("advapi32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, out LUID lpLuid);

    [DllImport("advapi32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
#pragma warning disable SA1313 // Parameter names should begin with lower-case letter
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, uint BufferLength, IntPtr PreviousState, IntPtr ReturnLength);
#pragma warning restore SA1313 // Parameter names should begin with lower-case letter

    private void ShowWindow(object? args)
    {
        if (args == null && this.ServiceHandle == IntPtr.Zero)
        {
            var t = Thread.CurrentThread;
            t.SetApartmentState(ApartmentState.Unknown);
            t.SetApartmentState(ApartmentState.STA);
            new Application().Run(new WINtpMainWindow(Wintp, this));
        }
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
        ulong ticks = (ulong)DateTime.UtcNow.Subtract(TimeOf1900).Ticks;
        ulong seconds = ticks / TimeSpan.TicksPerSecond;
        ulong fraction = ((ticks % TimeSpan.TicksPerSecond) << 32) / TimeSpan.TicksPerSecond;
        BitConverter.GetBytes(IPAddress.HostToNetworkOrder((long)(seconds << 32 | fraction))).CopyTo(data, 24);
        udp.SendTo(data, new IPEndPoint(Dns.GetHostAddresses(ntpServerUrl).FirstOrDefault(), 123));
        udp.Receive(data);
        if (data[1] < 16)
        {
            ulong t3 = (ulong)IPAddress.NetworkToHostOrder(BitConverter.ToInt64(data, 40));
            seconds = t3 >> 32;
            fraction = (uint)t3;
            ulong milliseconds = (seconds * 1000UL) + ((fraction * 1000UL) >> 32);
            return TimeOf1900.AddMilliseconds(milliseconds).ToUniversalTime();
        }

        throw new SocketException();
    }

    private DateTime GetHttpTime(string httpUrl)
    {
        if (!httpUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            httpUrl = $"{(mConfig.UseSsl ? "https://" : "http://")}{httpUrl}";
        }

        var req = (HttpWebRequest)WebRequest.Create(httpUrl);
        req.Method = "HEAD";
        req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36 Edg/143.0.0.0";
        req.Timeout = netTimeout;
        req.ReadWriteTimeout = netTimeout;
        req.ContinueTimeout = netTimeout;
        using var httpResposne = (HttpWebResponse)req.GetResponse();
        return Convert.ToDateTime(httpResposne.Headers["Date"]).ToUniversalTime();
    }

    private void LogPrintln(string errorMsg, int errorCode)
    {
        if (mConfig.Verbose)
        {
            try
            {
                using EventLog log = new("Application", ".", errorMsg);
                log.WriteEntry($"程序路径: {Wintp.Location}\r\n代码: {errorCode}, 可在 https://wintp.sourceforge.io/ 获取帮助。", EventLogEntryType.Error);
            }
            catch
            {
            }
        }
    }

    private void LoadProfileOrGetNetworkTime(object? args)
    {
        if (args is TimeSynchronizationOptions config)
        {
            var useForcedTime = false;
            var evt = config.ResetEvent;
            var serverUrl = config.HostName;
            if (evt != null && serverUrl != null)
            {
                DateTime? time = null;
                var isNtp = config.RequestType == NtpRequestType;
                if (isNtp && mConfig.Agreement == 1)
                {
                    return;
                }

                if (!isNtp && mConfig.Agreement == 0)
                {
                    return;
                }

                while (complete == 0)
                {
                    try
                    {
                        time = isNtp ? this.GetNtpTime(serverUrl) : this.GetHttpTime(serverUrl);
                        break;
                    }
                    catch
                    {
                        LogPrintln($"Unable to connect to the server({serverUrl}). Failed to sync network time.", NetworkConnectionError);
                        Thread.Sleep(1000);
                    }
                }

                if (Interlocked.CompareExchange(ref complete, 1, 0) != 0)
                {
                    Console.WriteLine($"Ignored: {serverUrl}");
                    Thread.CurrentThread.Abort();
                    return;
                }

                if (time is DateTime t)
                {
                    var offset = mConfig.Offset;
                    if (offset != 0)
                    {
                        t = t.AddMilliseconds(offset * 1000);
                    }

                    DateTime localTime = t.ToLocalTime(); // 注意，这个 localTime 表示由网络时间转换的本地时间，本地电脑时间使用 DateTime.Now 获取
                    var localPCTime = DateTime.Now;
                    var d = Math.Abs((localTime - localPCTime).TotalMilliseconds);
                    var deviationOffset = mConfig.DeviationOffset * 1000;
                    if (d < deviationOffset)
                    {
                        if (mConfig.Verbose)
                        {
                            Console.WriteLine($"Current Deviation: {d}, Max Deviation: {deviationOffset}, Skip Time Sync");
                        }

                        return;
                    }

                    if (localTime >= localPCTime)
                    {
                        switch (mConfig.SyncMode)
                        {
                            case 1:
                                useForcedTime = true;
                                break;
                            case 2:
                                var unstableIsSupported = false;
                                try
                                {
                                    unstableIsSupported = SetSystemTimeUnstable(localTime);
                                }
                                catch
                                {
                                    if (mConfig.Verbose)
                                    {
                                        Console.WriteLine("SetSystemTimeUnstable Not Supported!");
                                    }
                                }

                                if (!unstableIsSupported)
                                {
                                    useForcedTime = true;
                                }
                                else if (mConfig.Verbose)
                                {
                                    var l = DateTime.Now;
                                    Console.WriteLine($"[Unstable] Sync: {serverUrl}, Time: {localTime} {localTime.Millisecond}\r\n\tLocal Time: {l} {l.Millisecond}");
                                }

                                break;
                            case 3:
                                if (!SetSystemTimeLegacy(localTime))
                                {
                                    useForcedTime = true;
                                }
                                else if (mConfig.Verbose)
                                {
                                    var l = DateTime.Now;
                                    Console.WriteLine($"[Legacy] Sync: {serverUrl}, Time: {localTime} {localTime.Millisecond}\r\n\tLocal Time: {l} {l.Millisecond}");
                                }

                                break;
                            default:
                                if (mConfig.Verbose)
                                {
                                    Console.WriteLine("Stop TimeSync");
                                }

                                break;
                        }
                    }
                    else
                    {
                        useForcedTime = true;
                    }

                    if (useForcedTime)
                    {
                        st.Year = (short)t.Year;
                        st.Month = (short)t.Month;
                        st.Day = (short)t.Day;
                        st.Hour = (short)t.Hour;
                        st.Minute = (short)t.Minute;
                        st.Second = (short)t.Second;
                        st.Millisecond = (short)t.Millisecond;
                        _ = RequestPermission();
                        SetSystemTime(ref st);
                        if (mConfig.Verbose)
                        {
                            var deviationMs = localPCTime - localTime;
                            var l = DateTime.Now;
                            Console.WriteLine($"[Force Set] Sync: {serverUrl}, Time: {l} {l.Millisecond}");
                        }
                    }
                }

                evt.Set();
            }
        }
        else
        {
            var hosts = mConfig.Hosts;
            if (mConfig.Verbose)
            {
                Console.WriteLine("Verbose: {0}", mConfig.Verbose);
                Console.WriteLine("SyncMode: {0}", mConfig.SyncMode);
                Console.WriteLine("UseSsl: {0}", mConfig.UseSsl);
                Console.WriteLine("Timeout: {0}", mConfig.Timeout);
                Console.WriteLine("NetworkTimeout: {0}", mConfig.NetworkTimeout);
                Console.WriteLine("Delay: {0}", mConfig.Delay);
                Console.WriteLine("Agreement: {0}", mConfig.Agreement);
                if (hosts != null)
                {
                    foreach (var item in hosts)
                    {
                        Console.WriteLine("Server: {0}, Type: {1}", item.Key, item.Value.Type);
                    }
                }
            }

            List<TimeSynchronizationOptions> configs = [];
            if (hosts?.Count > 0)
            {
                foreach (var it in hosts)
                {
                    if (it.Value.Enabled)
                    {
                        configs.Add(new() { HostName = it.Key, RequestType = it.Value.Type, Priority = it.Value.Priority });
                    }
                    else if (mConfig.Verbose)
                    {
                        Console.WriteLine($"Disabled: {it.Key}");
                    }
                }
            }

            if (configs.Count > 0)
            {
                complete = 0;
                ManualResetEventSlim evt = new();
                configs.Sort((x, y) => x.Priority.CompareTo(y.Priority));
                foreach (var it in configs)
                {
                    it.ResetEvent = evt;
                    ThreadPool.UnsafeQueueUserWorkItem(this.LoadProfileOrGetNetworkTime, it);
                }

                ShowWindow(args);
                int timeout = (int)mConfig.Timeout;
                if (!evt.Wait(timeout < 1 ? 30000 : timeout) && this.ServiceHandle == IntPtr.Zero)
                {
                    LogPrintln("Timeout occurred while fetching network time. Please check the logs for details.", TimeoutError);
                    Environment.FailFast(string.Empty);
                }

                if (args != null && this.timer == null)
                {
                    int delay = (int)mConfig.Delay * 1000;
                    if (delay < 1)
                    {
                        Thread.Sleep(3000);
                        this.Stop();
                    }
                    else
                    {
                        this.timer = new(_ => this.OnStart(null), null, delay, delay);
                    }
                }
            }
            else
            {
                ShowWindow(args);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID_AND_ATTRIBUTES
    {
        public LUID Luid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_PRIVILEGES
    {
        public uint PrivilegeCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public LUID_AND_ATTRIBUTES[] Privileges;
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

        public int Priority { get; set; }

        public ManualResetEventSlim? ResetEvent { get; set; }
    }

    [DataContract]
    public class WINtpServiceConfig
    {
        [DataMember(Name = "syncMode")]
        public int SyncMode { get; set; }

        [DataMember(Name = "offset")]
        public double Offset { get; set; }

        [DataMember(Name = "deviationOffset")]
        public double DeviationOffset { get; set; }

        [DataMember(Name = "verbose")]
        public bool Verbose { get; set; }

        [DataMember(Name = "useSsl")]
        public bool UseSsl { get; set; }

        [DataMember(Name = "delay")]
        public decimal Delay { get; set; }

        [DataMember(Name = "timeout")]
        public decimal Timeout { get; set; }

        [DataMember(Name = "networkTimeout")]
        public decimal NetworkTimeout { get; set; }

        [DataMember(Name = "agreement")]
        public int Agreement { get; set; }

        [DataMember(Name = "hosts")]
        public Dictionary<string, HostValue>? Hosts { get; set; }

        [DataContract]
        public class HostValue(int type, int priority, bool enabled)
        {
            [DataMember(Name = "type")]
            public int Type { get; set; } = type;

            [DataMember(Name = "priority")]
            public int Priority { get; set; } = priority;

            [DataMember(Name = "enabled")]
            public bool Enabled { get; set; } = enabled;
        }
    }
}