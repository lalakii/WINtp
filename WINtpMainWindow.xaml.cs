using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;
using WinForms = System.Windows.Forms;

public partial class WINtpMainWindow : Window
{
    public WINtpMainWindow()
    {
        WinForms.Application.EnableVisualStyles();
        InitializeComponent();
    }

    public WINtpMainWindow(List<WINtp.TimeSynchronizationOptions> configs)
        : this()
    {
        var cl = WinForms.InputLanguage.CurrentInputLanguage.Culture;
        var en = !cl.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        string[] modes = ["仅 NTP", "仅 HTTP", "NTP 和 HTTP"];
        if (en)
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
            millsecondTbk0.Text = millsecondTbk1.Text = "milliseconds";
            secondTbk.Text = "seconds";
            installBtn.Content = "Install Service";
            uninstallBtn.Content = "Uninstall Service";
            Title = "WINtp Settings - ";
            moreTbk.Text = "Get Help: ";
            modeTbk.Text = "Time Sync Protocol: ";
            modes = ["NTP Only", "HTTP Only", "NTP and HTTP"];
        }

        var asm = typeof(WINtp).Assembly;
        Title += $" Version {asm.GetName().Version}";
        foreach (string it in modes)
        {
            modeCbx.Items.Add(it);
        }

        DataSet ds = new();
        var cbxDt = ds.Tables.Add("cbx_table");
        cbxDt.Columns.Add("Id");
        cbxDt.Columns.Add("Type");
        cbxDt.Rows.Add(WINtp.NtpRequestType, "NTP");
        cbxDt.Rows.Add(WINtp.HttpRequestType, "HTTP");
        typeListCbx.ItemsSource = cbxDt.DefaultView;
        var listDt = ds.Tables.Add("list_table");
        listDt.Columns.Add("Url");
        listDt.Columns.Add("TypeId");
        foreach (var it in configs)
        {
            listDt.Rows.Add(it.HostName, it.RequestType);
        }

        serverListDgd.ItemsSource = listDt.DefaultView;
        timeoutNud.Maximum = netTimeoutNud.Maximum = timeSyncNud.Maximum = int.MaxValue;
        modeCbx.SelectionChanged += (_, e) => useSslCbx.IsEnabled = modeCbx.SelectedIndex != 0;
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
                case "Agreement":
                    if (decimal.TryParse(it.Value, out numberValue))
                    {
                        switch (numberValue)
                        {
                            case 0:
                            case 1:
                            case 2:
                                modeCbx.SelectedIndex = (int)numberValue;
                                break;
                        }
                    }

                    break;
            }
        }

        currentTimeDpk.CustomFormat = cl.DateTimeFormat.FullDateTimePattern;
        WinForms.Timer t = new() { Interval = 300, };
        t.Tick += (_, _) => currentTimeDpk.Value = DateTime.Now;
        Loaded += (_, _) => t.Start();
        Closing += (_, _) =>
        {
            Hide();
            t.Stop();
            Application.Current.Shutdown();
            Environment.Exit(0);
        };
        var errorMsg = en ? "Missing file: \"{0}\"!" : "找不到文件: \"{0}\"!";
        applyBtn.Click += (_, _) =>
        {
            serverListDgd.CommitEdit();
            string ntpUrl = string.Empty;
            string httpUrl = string.Empty;
            foreach (DataRow it in listDt.Rows)
            {
                if (int.TryParse($"{it[1]}", out int type) && type == WINtp.HttpRequestType)
                {
                    httpUrl += $"{it[0]};";
                }
                else
                {
                    ntpUrl += $"{it[0]};";
                }
            }

            var appSettings = pCfg.AppSettings.Settings;
            SetValue(appSettings, "AutoSyncTime", $"{autoSyncCbx.IsChecked}");
            SetValue(appSettings, "Verbose", $"{logPrintCbx.IsChecked}");
            SetValue(appSettings, "UseSsl", $"{useSslCbx.IsChecked}");
            SetValue(appSettings, "Timeout", $"{timeoutNud.Value}");
            SetValue(appSettings, "NetworkTimeout", $"{netTimeoutNud.Value}");
            SetValue(appSettings, "Delay", $"{timeSyncNud.Value}");
            SetValue(appSettings, "Agreement", $"{modeCbx.SelectedIndex}");
            SetValue(appSettings, "Ntps", ntpUrl);
            SetValue(appSettings, "Urls", httpUrl);
            pCfg.Save(ConfigurationSaveMode.Modified);
            RunAsBatFile(asm.Location, "wRestart.bat", errorMsg);
            MessageBox.Show(en ? "Configuration has been saved!" : "配置已存储!", string.Empty, MessageBoxButton.OK, MessageBoxImage.Information);
        };
        installBtn.Click += (_, _) => RunAsBatFile(asm.Location, "wInstall.bat", errorMsg);
        uninstallBtn.Click += (_, _) => RunAsBatFile(asm.Location, "wUninstall.bat", errorMsg);
        exitBtn.Click += (_, _) => Close();
    }

    private static void RunAsBatFile(string exePath, string batName, string errorMsg)
    {
        var scriptBat = Path.Combine(Path.GetDirectoryName(exePath), "Scripts", batName);
        if (File.Exists(scriptBat))
        {
            Process.Start(scriptBat);
        }
        else
        {
            MessageBox.Show(string.Format(errorMsg, scriptBat), null, MessageBoxButton.OK, MessageBoxImage.Error);
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

    private void Navigate(object o, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        Process.Start(e.Uri.AbsoluteUri);
    }
}