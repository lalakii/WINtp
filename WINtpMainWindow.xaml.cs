using System.Data;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using WinForms = System.Windows.Forms;

public partial class WINtpMainWindow : Window
{
    public WINtpMainWindow()
    {
        WinForms.Application.EnableVisualStyles();
        InitializeComponent();
    }

    public WINtpMainWindow(WINtp wintp, Assembly asm)
        : this()
    {
        var cl = WinForms.InputLanguage.CurrentInputLanguage.Culture;
        var en = !cl.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        string[] modes = ["仅 NTP", "仅 HTTP", "NTP 和 HTTP"];
        string[] syncModes = ["停止同步时间", "立即修改时间", "渐进加速优先(高精度)", "渐进加速优先"];
        var secondText = "秒";
        var msText = "毫秒";
        var addText = "添加";
        if (en)
        {
            applyBtn.Content = "Save";
            exitBtn.Content = "Quit";
            useSslCbx.Content = "Use SSL";
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
            syncModes = ["Stop Time Sync", "Set Time Immediately", "Prefer Gradual Adjustment (High Precision)", "Prefer Gradual Adjustment"];
            secondText = "s";
            msText = "ms";
            addText = "Add";
            offsetResetBtn.Content = deviationResetBtn.Content = "Reset";
            syncModeTbk.Text = "Synchronization Mode: ";
            offsetTbk.Text = "Time Offset: ";
            deviationTbk.Text = "Tolerance: ";
            priorityDgc.Header = "Priority";
            enabledDgc.Header = "Enabled";
            disableWin32TimeSyncCbx.Content = "Disable Windows Time Service";
        }

        Title += $" Version {asm.GetName().Version}";
        foreach (string it in modes)
        {
            modeCbx.Items.Add(it);
        }

        foreach (string it in syncModes)
        {
            syncModeCbx.Items.Add(it);
        }

        modeCbx.SelectionChanged += (_, _) => useSslCbx.IsEnabled = modeCbx.SelectedIndex != 0;
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
        listDt.Columns.Add("Priority");
        listDt.Columns.Add("Enabled");
        serverListDgd.ItemsSource = listDt.DefaultView;
        timeoutNud.Maximum = netTimeoutNud.Maximum = timeSyncNud.Maximum = int.MaxValue;
        var config = wintp.GetConfig();
        if (config != null)
        {
            if (config.Hosts != null)
            {
                foreach (var host in config.Hosts)
                {
                    listDt.Rows.Add(host.Key, host.Value.Type, host.Value.Priority, host.Value.Enabled);
                }
            }

            switch (config.SyncMode)
            {
                case 0:
                case 1:
                case 2:
                case 3:
                    syncModeCbx.SelectedIndex = config.SyncMode;
                    break;
            }

            offsetSdr.Value = config.Offset;
            deviationSdr.Value = config.DeviationOffset;
            switch (config.Agreement)
            {
                case 0:
                case 1:
                case 2:
                    modeCbx.SelectedIndex = config.Agreement;
                    break;
            }

            timeoutNud.Value = config.Timeout;
            netTimeoutNud.Value = config.NetworkTimeout;
            timeSyncNud.Value = config.Delay;
            logPrintCbx.IsChecked = config.Verbose;
            useSslCbx.IsChecked = config.UseSsl;
            disableWin32TimeSyncCbx.IsChecked = config.DisableWin32Time;
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
        offsetResetBtn.Click += (_, _) => offsetSdr.Value = 0;
        deviationResetBtn.Click += (_, _) => deviationSdr.Value = 0;
        var errorMsg = en ? "Missing file: \"{0}\"!" : "找不到文件: \"{0}\"!";
        applyBtn.Click += (_, _) =>
        {
            if (config != null)
            {
                serverListDgd.CommitEdit();
                Dictionary<string, WINtp.WINtpServiceConfig.HostValue> newHosts = [];
                foreach (DataRow it in listDt.Rows)
                {
                    var host = $"{it[0]}";
                    if (!string.IsNullOrWhiteSpace(host) && int.TryParse($"{it[1]}", out int type))
                    {
                        _ = int.TryParse($"{it[2]}", out int priority);
                        _ = bool.TryParse($"{it[3]}", out bool enabled);
                        newHosts[host] = new(type, priority, enabled);
                    }
                }

                config.SyncMode = syncModeCbx.SelectedIndex;
                config.Offset = offsetSdr.Value;
                config.DeviationOffset = deviationSdr.Value;
                config.Agreement = modeCbx.SelectedIndex;
                config.Timeout = timeoutNud.Value;
                config.NetworkTimeout = netTimeoutNud.Value;
                config.Delay = timeSyncNud.Value;
                config.Verbose = logPrintCbx.IsChecked == true;
                config.UseSsl = useSslCbx.IsChecked == true;
                config.DisableWin32Time = disableWin32TimeSyncCbx.IsChecked == true;
                config.Hosts = newHosts;
                wintp.SaveConfig();
            }

            RunAsBatFile(asm.Location, "wRestart.bat", errorMsg);
            MessageBox.Show(en ? "Configuration has been saved!" : "配置已存储!", string.Empty, MessageBoxButton.OK, MessageBoxImage.Information);
        };

        ContextMenu cm = new();
        serverListDgd.ContextMenu = cm;
        MenuItem addMi = new() { Header = addText };
        addMi.Click += (_, _) =>
        {
            var newRow = listDt.NewRow();
            newRow[1] = 0;
            newRow[2] = 0;
            newRow[3] = true;
            listDt.Rows.Add(newRow);
        };
        cm.Items.Add(addMi);
        offsetSdr.ValueChanged += (_, _) => offsetValueTbk.Text = FormatStringValue(offsetSdr.Value, secondText, msText);
        offsetValueTbk.Text = FormatStringValue(offsetSdr.Value, secondText, msText);
        deviationSdr.ValueChanged += (_, _) => deviationValueTbk.Text = FormatStringValue2(deviationSdr.Value, secondText, msText);
        deviationValueTbk.Text = FormatStringValue2(deviationSdr.Value, secondText, msText);
        installBtn.Click += (_, _) => RunAsBatFile(asm.Location, "wInstall.bat", errorMsg);
        uninstallBtn.Click += (_, _) => RunAsBatFile(asm.Location, "wUninstall.bat", errorMsg);
        exitBtn.Click += (_, _) => Close();
    }

    private static string FormatStringValue2(double value, string secondText, string msText)
    {
        return $"±{value:F3} {secondText} = ±{value * 1000:F0} {msText}";
    }

    private static string FormatStringValue(double value, string secondText, string msText)
    {
        string plus = string.Empty;
        if (value > 0)
        {
            plus = "+";
        }

        return $"{plus}{value:F3} {secondText} = {plus}{value * 1000:F0} {msText}";
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

    private void Navigate(object o, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        Process.Start(e.Uri.AbsoluteUri);
    }
}