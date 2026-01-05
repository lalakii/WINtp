using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;

public partial class WINtpMainWindow : Window
{
    public WINtpMainWindow()
    {
        System.Windows.Forms.Application.EnableVisualStyles();
        InitializeComponent();
    }

    public WINtpMainWindow(List<WINtp.TimeSynchronizationOptions> configs)
        : this()
    {
        var cl = System.Windows.Forms.InputLanguage.CurrentInputLanguage.Culture;
        var cn = cl.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        if (!cn)
        {
            applyBtn.Content = "Save";
            exitBtn.Content = "Quit";
            useSslCbx.Content = "Use SSL";
            autoSyncCbx.Content = "Automatically synchronize time";
            logPrintCbx.Content = "Log output";
            domainDgc.Header = "Hostname";
            typeListCbx.Header = "Protocol";
            serverListTbk.Header = "Server List";
            sysTimeTbk.Text = "System Time: ";
            configTbk.Header = "Configuration";
            netTimeoutTbk.Text = "Network Timeout: ";
            timeoutTbk.Text = "Sync Timeout: ";
            intervalTbk.Text = "Sync Interval: ";
            millsecondTbk0.Text = millsecondTbk1.Text = "ms";
            secondTbk.Text = "s";
            installBtn.Content = "Install Service";
            uninstallBtn.Content = "Uninstall Service";
            Title = "WINtp Settings - ";
            moreTbk.Text = "Get Help: ";
        }

        var asm = typeof(WINtp).Assembly;
        Title += $" Version {asm.GetName().Version}";
        currentTimeDpk.CustomFormat = cl.DateTimeFormat.FullDateTimePattern;
        DataTable dt1 = new();
        dt1.Columns.Add("Id");
        dt1.Columns.Add("Type");
        dt1.Rows.Add(WINtp.NtpRequestType, "NTP");
        dt1.Rows.Add(WINtp.HttpRequestType, "HTTP");
        typeListCbx.DisplayMemberPath = "Type";
        typeListCbx.SelectedValuePath = "Id";
        typeListCbx.ItemsSource = dt1.DefaultView;
        DataTable dt = new();
        dt.Columns.Add("Url");
        dt.Columns.Add("Type");
        serverListDgd.ItemsSource = dt.DefaultView;
        foreach (var item in configs)
        {
            dt.Rows.Add(item.HostName, item.RequestType);
        }

        timeoutNud.Maximum = netTimeoutNud.Maximum = timeSyncNud.Maximum = int.MaxValue;
        var pCfg = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        foreach (KeyValueConfigurationElement it in pCfg.AppSettings.Settings)
        {
            bool isChecked;
            decimal numberValue;
            switch (it.Key)
            {
                case "AutoSyncTime":
                    if (bool.TryParse(it.Value, out isChecked))
                    {
                        autoSyncCbx.IsChecked = isChecked;
                    }

                    break;
                case "Verbose":
                    if (bool.TryParse(it.Value, out isChecked))
                    {
                        logPrintCbx.IsChecked = isChecked;
                    }

                    break;
                case "UseSsl":
                    if (bool.TryParse(it.Value, out isChecked))
                    {
                        useSslCbx.IsChecked = isChecked;
                    }

                    break;
                case "Timeout":
                    if (decimal.TryParse(it.Value, out numberValue))
                    {
                        timeoutNud.Value = numberValue;
                    }

                    break;
                case "NetworkTimeout":
                    if (decimal.TryParse(it.Value, out numberValue))
                    {
                        netTimeoutNud.Value = numberValue;
                    }

                    break;
                case "Delay":
                    if (decimal.TryParse(it.Value, out numberValue))
                    {
                        timeSyncNud.Value = numberValue;
                    }

                    break;
            }
        }

        System.Windows.Forms.Timer t = new() { Interval = 300, };
        t.Tick += (_, _) => currentTimeDpk.Value = DateTime.Now;
        Loaded += (_, _) => t.Start();
        Closing += (_, _) =>
        {
            Hide();
            t.Stop();
            Application.Current.Shutdown();
            Environment.Exit(0);
        };
        applyBtn.Click += (_, _) =>
        {
            serverListDgd.CommitEdit();
            string ntpUrl = string.Empty;
            string httpUrl = string.Empty;
            foreach (DataRow item in dt.Rows)
            {
                if (int.TryParse($"{item[1]}", out int type) && type == WINtp.HttpRequestType)
                {
                    httpUrl += $"{item[0]};";
                }
                else
                {
                    ntpUrl += $"{item[0]};";
                }
            }

            var appSettings = pCfg.AppSettings.Settings;
            SetValue(appSettings, "AutoSyncTime", $"{autoSyncCbx.IsChecked}");
            SetValue(appSettings, "Verbose", $"{logPrintCbx.IsChecked}");
            SetValue(appSettings, "UseSsl", $"{useSslCbx.IsChecked}");
            SetValue(appSettings, "Timeout", $"{timeoutNud.Value}");
            SetValue(appSettings, "NetworkTimeout", $"{netTimeoutNud.Value}");
            SetValue(appSettings, "Delay", $"{timeSyncNud.Value}");
            SetValue(appSettings, "Ntps", ntpUrl);
            SetValue(appSettings, "Urls", httpUrl);
            pCfg.Save(ConfigurationSaveMode.Modified);
            MessageBox.Show(cn ? "配置已存储!" : "Configuration has been saved!", string.Empty, MessageBoxButton.OK, MessageBoxImage.Information);
        };
        exitBtn.Click += (_, _) =>
        {
            Close();
        };
        homeUri.RequestNavigate += (_, e) => Process.Start(e.Uri.AbsoluteUri);
        homeUri2.RequestNavigate += (_, e) => Process.Start(e.Uri.AbsoluteUri);
        var errorMsg = cn ? "找不到文件: \"{0}\"!" : "Missing file: \"{0}\"!";
        installBtn.Click += (_, _) => RunAsBatFile(asm.Location, "wInstall.bat", errorMsg);
        uninstallBtn.Click += (_, _) => RunAsBatFile(asm.Location, "wUninstall.bat", errorMsg);
    }

    private static void RunAsBatFile(string exePath, string batName, string errorMsg)
    {
        var scriptBat = Path.Combine(Path.GetDirectoryName(exePath), batName);
        if (File.Exists(scriptBat))
        {
            Process.Start(scriptBat);
        }
        else
        {
            MessageBox.Show(string.Format(errorMsg, batName), null, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void SetValue(KeyValueConfigurationCollection collection, string key, string value)
    {
        if (collection.AllKeys.Contains(key))
        {
            collection[key].Value = value;
        }
        else
        {
            collection.Add(key, value);
        }
    }
}