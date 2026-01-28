using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Windows;

[assembly: AssemblyProduct("WINtp")]
[assembly: AssemblyVersion("2.5.0.0")]
[assembly: AssemblyFileVersion("2.5.0.0")]
[assembly: AssemblyTitle("A Simple NTP Client")]
[assembly: AssemblyCopyright("Copyright (C) 2026 lalaki.cn")]

public class WINtp(WINtp.WINtpServiceConfig serviceConfig, int netTimeout) : System.ServiceProcess.ServiceBase
{
    public const int ProgressiveDelay = 800;
    public const int NtpRequestType = 0;
    public const int HttpRequestType = 1;
#pragma warning disable SA1310 // Field names should not contain underscore
    private const string SE_SYSTEMTIME_NAME = "SeSystemtimePrivilege";
    private const uint SE_PRIVILEGE_ENABLED = 0x00000002;
    private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    private const uint TOKEN_QUERY = 0x0008;
#pragma warning restore SA1310 // Field names should not contain underscore
    private static readonly string[] ServiceParamsArray = ["-k", "/k"];
    private static readonly string[] NormalParamsArray = ["-d", "/d"];
    private static readonly DateTime TimeOf1900 = new(1900, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Assembly Wintp = typeof(WINtp).Assembly;
    private static readonly string ConfigPath = $"{Wintp.Location}.json";
    private static bool permissionIsGranted = false;
    private static SystemTime st;
    private Timer? timer;
    private int complete;

    private enum WINtpError
    {
        NetworkConnection = -3,
        Timeout,
    }

    private enum TOKEN_INFORMATION_CLASS
    {
        TokenUser = 1,
        TokenGroups,
        TokenPrivileges,
    }

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
                    Console.WriteLine("E: JSON Serialization Failed!");
                }
            }

            if (pConfig == null)
            {
                pConfig = new()
                {
                    SyncMode = 0,
                    Offset = 0,
                    DeviationOffset = 0,
                    Agreement = 0,
                    Timeout = 30000,
                    NetworkTimeout = 5000,
                    Delay = 3600,
                    UseSsl = false,
                    Verbose = false,
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/144.0.0.0 Safari/537.36 Edg/144.0.0.0",
                    Hosts = new() { ["ntp.tencent.com"] = new(0, 0, true), ["ntp.aliyun.com"] = new(0, 0, true), ["time.cloudflare.com"] = new(0, 0, true), ["time.asia.apple.com"] = new(0, 0, true), ["rhel.pool.ntp.org"] = new(0, 0, true), ["www.baidu.com"] = new(1, 1, true), ["www.qq.com"] = new(1, 1, true), ["www.163.com"] = new(1, 1, true), },
                };
                json.SetLength(0);
                serializer.WriteObject(json, pConfig);
            }
        }

        using WINtp ntp = new(pConfig, (int)pConfig.NetworkTimeout);
        var userStarted = false;
        if (args is string[] argments && argments.Length > 0)
        {
            if (ServiceParamsArray.Contains(argments.LastOrDefault(), StringComparer.OrdinalIgnoreCase))
            {
                Run(ntp);
            }
            else if (NormalParamsArray.Contains(argments.LastOrDefault(), StringComparer.OrdinalIgnoreCase))
            {
                ntp.LoadProfileOrGetNetworkTime(0);
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
            if (pConfig.Verbose && AllocConsole())
            {
                var asmName = Wintp.GetName();
                Console.Title = $"{asmName.Name} Logcat - Version: {asmName.Version}";
            }

            ntp.LoadProfileOrGetNetworkTime(null);
        }
    }

    public WINtpServiceConfig GetConfig()
    {
        return serviceConfig;
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
        serializer.WriteObject(json, serviceConfig);
        Verbose("SaveConfig");
    }

    protected override void OnStart(string[]? args)
    {
        base.OnStart(args);
        this.LoadProfileOrGetNetworkTime(new object?[] { null });
        LogPrintln("INFO: WINtp Service Started", 0);
    }

    protected override void OnStop()
    {
        LogPrintln("INFO: WINtp Service Stopped", 0);
        this.timer?.Dispose();
        base.OnStop();
    }

    private static bool RequestPermission()
    {
        if (!permissionIsGranted && OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out IntPtr hToken) && LookupPrivilegeValue(IntPtr.Zero, new(SE_SYSTEMTIME_NAME), out LUID luid))
        {
            _ = GetTokenInformation(hToken, TOKEN_INFORMATION_CLASS.TokenPrivileges, IntPtr.Zero, 0, out uint returnLength);
            if (returnLength > 0)
            {
                var pTokenPrivs = Marshal.AllocHGlobal((int)returnLength);
                if (GetTokenInformation(hToken, TOKEN_INFORMATION_CLASS.TokenPrivileges, pTokenPrivs, returnLength, out _))
                {
                    var tp = Marshal.PtrToStructure<TOKEN_PRIVILEGES>(pTokenPrivs);
                    var item = pTokenPrivs + sizeof(uint);
                    int itemSize = Marshal.SizeOf<LUID_AND_ATTRIBUTES>();
                    for (int i = 0; i < tp.PrivilegeCount; i++)
                    {
                        var privilege = Marshal.PtrToStructure<LUID_AND_ATTRIBUTES>(item);
                        var outLuid = privilege.Luid;
                        uint cchName = 100;
                        StringBuilder name = new((int)cchName);
                        if (luid.LowPart == outLuid.LowPart && luid.HighPart == outLuid.HighPart && LookupPrivilegeName(IntPtr.Zero, ref outLuid, name, ref cchName))
                        {
                            permissionIsGranted = (privilege.Attributes & SE_PRIVILEGE_ENABLED) != 0;
                            if (!permissionIsGranted)
                            {
                                tp = new() { PrivilegeCount = 1, Privileges = [new() { Luid = luid, Attributes = SE_PRIVILEGE_ENABLED }] };
                                permissionIsGranted = AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
                                Console.WriteLine($"I: PermissionIsGranted: {SE_SYSTEMTIME_NAME}");
                            }

                            break;
                        }

                        item += itemSize;
                    }
                }

                Marshal.FreeHGlobal(pTokenPrivs);
            }

            _ = CloseHandle(hToken);
        }
        else if (permissionIsGranted)
        {
            Console.WriteLine("I: PermissionIsGranted: Skip");
        }

        return permissionIsGranted;
    }

    private static bool SetSystemTimeUnstable(DateTime t)
    {
        bool supported = true;
        if (RequestPermission() && GetSystemTimeAdjustmentPrecise(out _, out ulong lpTimeIncrement, out _))
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
        if (RequestPermission() && GetSystemTimeAdjustment(out _, out uint lpTimeIncrement, out _))
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

    [DllImport("kernel32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GetCurrentProcess();

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

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool LookupPrivilegeValue(IntPtr lpSystemName, StringBuilder lpName, out LUID lpLuid);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool LookupPrivilegeName(IntPtr lpSystemName, ref LUID lpLuid, StringBuilder lpName, ref uint cchName);

    [DllImport("advapi32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
#pragma warning disable SA1313 // Parameter names should begin with lower-case letter
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool GetTokenInformation(IntPtr TokenHandle, TOKEN_INFORMATION_CLASS TokenInformationClass, IntPtr TokenInformation, uint TokenInformationLength, out uint ReturnLength);

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
            new Application().Run(new WINtpMainWindow(this, Wintp));
        }
    }

    private DateTime GetNtpTime(string ntpServerUrl)
    {
        var data = new byte[48];
        data[0] = 0x1B;
        using Socket udp = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
        {
            SendTimeout = netTimeout,
            ReceiveTimeout = netTimeout,
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

    private DateTime GetHttpTime(string url)
    {
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            url = $"{(serviceConfig.UseSsl ? "https://" : "http://")}{url}";
        }

        var req = (HttpWebRequest)WebRequest.Create(url);
        req.Method = "HEAD";
        req.UserAgent = serviceConfig.UserAgent;
        req.Timeout = netTimeout;
        req.ReadWriteTimeout = netTimeout;
        req.ContinueTimeout = netTimeout;
        using var httpResposne = (HttpWebResponse)req.GetResponse();
        return Convert.ToDateTime(httpResposne.Headers["Date"]).ToUniversalTime();
    }

    private void LogPrintln(string errorMsg, object errorCode)
    {
        if (serviceConfig.Verbose)
        {
            try
            {
                var len = errorMsg.Length;
                if (len > 50)
                {
                    len = 50;
                }

                using EventLog log = new("Application", ".", errorMsg.Substring(0, len - 1));
                log.WriteEntry($"程序路径: {Wintp.Location}\r\n代码: {errorCode}\r\n完整信息: {errorMsg}, 可在 https://wintp.sourceforge.io/ 获取帮助。", EventLogEntryType.Error);
            }
            catch (Exception e)
            {
                Console.WriteLine("E: EventLog Write Failed!" + e.Message);
            }
        }
    }

    private void Verbose(string msg)
    {
        if (serviceConfig.Verbose)
        {
            Console.WriteLine(msg);
        }
    }

    private void LoadProfileOrGetNetworkTime(object? args)
    {
        if (args is object[] objArgs && objArgs.Length == 1)
        {
            ThreadPool.UnsafeQueueUserWorkItem(this.LoadProfileOrGetNetworkTime, objArgs[0]);
        }
        else if (args is TimeSynchronizationOptions config)
        {
            var useForcedTime = false;
            var evt = config.ResetEvent;
            var serverUrl = config.HostName;
            if (evt != null && serverUrl != null)
            {
                DateTime? time = null;
                var isNtp = config.RequestType == NtpRequestType;
                if (isNtp && serviceConfig.Agreement == 1)
                {
                    return;
                }

                if (!isNtp && serviceConfig.Agreement == 0)
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
                        LogPrintln($"Unable to connect to the server({serverUrl}). Failed to sync network time.", WINtpError.NetworkConnection);
                        Thread.Sleep(1000);
                    }
                }

                if (Interlocked.CompareExchange(ref complete, 1, 0) != 0)
                {
                    Verbose($"Ignored: {serverUrl}");
                    return;
                }

                if (time is DateTime t)
                {
                    var offset = serviceConfig.Offset;
                    if (offset != 0)
                    {
                        t = t.AddMilliseconds(offset * 1000);
                    }

                    DateTime localTime = t.ToLocalTime(); // 注意，这个 localTime 表示由网络时间转换的本地时间，本地电脑时间使用 DateTime.Now 获取
                    var localPCTime = DateTime.Now;
                    var d = Math.Abs((localTime - localPCTime).TotalMilliseconds);
                    var deviationOffset = serviceConfig.DeviationOffset * 1000;
                    if (d < deviationOffset)
                    {
                        Verbose($"Current Deviation: {d}, Max Deviation: {deviationOffset}, Skip Time Sync");
                        return;
                    }

                    if (localTime >= localPCTime)
                    {
                        switch (serviceConfig.SyncMode)
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
                                    Verbose("E: SetSystemTimeUnstable Not Supported!");
                                }

                                if (!unstableIsSupported)
                                {
                                    useForcedTime = true;
                                    if (serviceConfig.HighPrecisionSupported != false)
                                    {
                                        serviceConfig.HighPrecisionSupported = false;
                                        SaveConfig();
                                    }
                                }
                                else
                                {
                                    if (serviceConfig.HighPrecisionSupported != true)
                                    {
                                        serviceConfig.HighPrecisionSupported = true;
                                        SaveConfig();
                                    }

                                    if (serviceConfig.Verbose)
                                    {
                                        var l = DateTime.Now;
                                        Console.WriteLine($"[Unstable] Sync: {serverUrl}, Time: {localTime} {localTime.Millisecond}\r\n\t   End Time: {l} {l.Millisecond}");
                                    }
                                }

                                Verbose($"HighPrecisionSupported: {serviceConfig.HighPrecisionSupported}");
                                break;
                            case 3:
                                if (!SetSystemTimeLegacy(localTime))
                                {
                                    useForcedTime = true;
                                }
                                else if (serviceConfig.Verbose)
                                {
                                    var l = DateTime.Now;
                                    Console.WriteLine($"[Legacy] Sync: {serverUrl}, Time: {localTime} {localTime.Millisecond}\r\n\t   End Time: {l} {l.Millisecond}");
                                }

                                break;
                            default:
                                Verbose("Stop TimeSync");
                                break;
                        }
                    }
                    else
                    {
                        useForcedTime = true;
                    }

                    if (useForcedTime)
                    {
                        st.Year = (ushort)t.Year;
                        st.Month = (ushort)t.Month;
                        st.Day = (ushort)t.Day;
                        st.Hour = (ushort)t.Hour;
                        st.Minute = (ushort)t.Minute;
                        st.Second = (ushort)t.Second;
                        st.Millisecond = (ushort)t.Millisecond;
                        _ = RequestPermission();
                        SetSystemTime(ref st);
                        if (serviceConfig.Verbose)
                        {
                            var deviationMs = localPCTime - localTime;
                            var l = DateTime.Now;
                            Console.WriteLine($"[Force Set] Sync: {serverUrl}, Time: {l} {l.Millisecond}");
                        }
                    }

                    LogPrintln($"INFO: [{DateTime.Now}] TimeSync Success", 0);
                    evt.Set();
                }
            }
        }
        else
        {
            var hosts = serviceConfig.Hosts;
            if (serviceConfig.Verbose)
            {
                StringBuilder info = new();
                info.AppendLine($"Verbose: {serviceConfig.Verbose}");
                info.AppendLine($"SyncMode: {serviceConfig.SyncMode}");
                info.AppendLine($"HighPrecisionSupported: {serviceConfig.HighPrecisionSupported}");
                info.AppendLine($"UseSsl: {serviceConfig.UseSsl}");
                info.AppendLine($"Timeout: {serviceConfig.Timeout}");
                info.AppendLine($"NetworkTimeout: {serviceConfig.NetworkTimeout}");
                info.AppendLine($"Delay: {serviceConfig.Delay}");
                info.AppendLine($"Agreement: {serviceConfig.Agreement}");
                info.AppendLine($"DisableWin32Time: {serviceConfig.DisableWin32Time}");
                info.AppendLine($"UserAgent: {serviceConfig.UserAgent}");
                if (hosts != null)
                {
                    foreach (var item in hosts)
                    {
                        info.AppendLine($"Server: {item.Key}, Type: {item.Value.Type} Priority: {item.Value.Priority}");
                    }
                }

                Console.WriteLine(info.ToString());
                LogPrintln(info.ToString(), 0);
            }

            var keyName = "SYSTEM\\CurrentControlSet\\Services\\W32Time\\Parameters";
            var type = serviceConfig.DisableWin32Time ? "NoSync" : "NTP";
            try
            {
                using var subKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyName, true);
                var oldType = $"{subKey.GetValue("Type", string.Empty)}";
                Verbose($"I: Windows Time Service OldType: {oldType}, NewType: {type}");
                if (!type.Equals(oldType, StringComparison.OrdinalIgnoreCase))
                {
                    subKey.SetValue("Type", type);
                    Verbose($"I: Windows Time Service Set Type: {type}");
                }
                else
                {
                    Verbose($"I: Windows Time Service Current Type: {type}");
                }
            }
            catch
            {
                Verbose($"E: Registry read/write failed!({keyName} - {type})");
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
                    else
                    {
                        Verbose($"Disabled: {it.Key}");
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
                    this.LoadProfileOrGetNetworkTime(new object[] { it });
                }

                ShowWindow(args);
                int timeout = (int)serviceConfig.Timeout;
                if (!evt.Wait(timeout < 1 ? 30000 : timeout) && this.ServiceHandle == IntPtr.Zero)
                {
                    LogPrintln("Timeout occurred while fetching network time. Please check the logs for details.", WINtpError.Timeout);
                    Environment.FailFast(string.Empty);
                }
                else
                {
                    LogPrintln($"INFO: WINtp Service Running, Time: {DateTime.Now}", 0);
                }

                if (args != null && this.timer == null)
                {
                    int delay = (int)serviceConfig.Delay * 1000;
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
        public ushort Year;
        public ushort Month;
        public ushort DayOfWeek;
        public ushort Day;
        public ushort Hour;
        public ushort Minute;
        public ushort Second;
        public ushort Millisecond;
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

        [DataMember(Name = "highPrecision")]
        public bool HighPrecisionSupported { get; set; }

        [DataMember(Name = "offset")]
        public double Offset { get; set; }

        [DataMember(Name = "deviationOffset")]
        public double DeviationOffset { get; set; }

        [DataMember(Name = "verbose")]
        public bool Verbose { get; set; }

        [DataMember(Name = "useSsl")]
        public bool UseSsl { get; set; }

        [DataMember(Name = "disableWin32Time")]
        public bool DisableWin32Time { get; set; }

        [DataMember(Name = "delay")]
        public decimal Delay { get; set; }

        [DataMember(Name = "timeout")]
        public decimal Timeout { get; set; }

        [DataMember(Name = "networkTimeout")]
        public decimal NetworkTimeout { get; set; }

        [DataMember(Name = "agreement")]
        public int Agreement { get; set; }

        [DataMember(Name = "userAgent")]
        public string? UserAgent { get; set; }

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