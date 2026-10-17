using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Brush = System.Windows.Media.Brush;

namespace LeigodPause.Views.Pages;

/// <summary>
/// Interaction logic for DashboardPage.xaml
/// </summary>
public partial class DashboardPage : INotifyPropertyChanged
{
    public string InstallButtonText => Utils.IsInstalled() ? "卸载服务" : "安装服务";
    private bool _isRunning = false;
    public string IsRunningText => IsRunning ? "运行中" : "未运行";
    public bool IsRunning => _isRunning;
    public Visibility RunningBtnVisible => (Utils.IsInstalled() && !IsRunning) ? Visibility.Visible : Visibility.Collapsed;


    private const string leigodLoginUrl = "https://www.leigod.com/mlogin";
    // "https://www.leigod.com/m/mlogin.html?region_code=1&language=zh_CN&platform=2";

    private const string leigodPersonalUrl = "https://www.leigod.com/personal-center";

    public LeigodCoreModel LeigodCoreModel { get; } = App.Host.Services.GetRequiredService<LeigodCoreModel>();

    private ILogger<DashboardPage> logger = App.Host.Services.GetRequiredService<ILogger<DashboardPage>>();


    public ImageSource? GameImageIcon => (App.CurrentApp.MainWindow as MainView)?.GameImageIcon;
    public Brush PauseStatusTextBrush => (Brush)App.CurrentApp.Resources[LeigodCoreModel?.IsNotPaused == true ? "SystemFillColorCautionBrush" : "SystemFillColorAttentionBrush"];

    public DashboardPage()
    {
        DataContext = this;
        InitializeComponent();

        // 不调用 this.ApplyTheme()：否则会给本页挂一个“构造时固定”的本地主题字典，
        // 就近遮蔽全局/主窗口的主题画刷，导致设置页切换主题时本页文字颜色不更新。
        // 文字颜色由 Page 上的 {DynamicResource TextFillColorPrimaryBrush} 提供，随主题动态刷新。

        // 把“退出清理（含 WebView 缓存）”注册给主窗口，由主窗口在『确认退出』后才调用；
        // 最小化到托盘不触发，避免误清缓存（原先 AppMainWindowOnClosing 每次关闭都清，是冲突根源）。
        var currentApp = App.CurrentApp;
        if (currentApp.MainWindow is MainView mainView)
        {
            mainView.RegisterExitCleanup(CleanupForExitAsync);
        }
    }

    private CancellationTokenSource? _pollCts;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 去掉 Timer：改为「任务完成后等待间隔再循环」，避免 Dispatcher.Invoke 的委托参数错配。
        _pollCts = new CancellationTokenSource();
        _ = PollLoopAsync(_pollCts.Token);

        LeigodCoreModel.PropertyChanged += LeigodCoreModelOnPropertyChanged;
    }

    private void LeigodCoreModelOnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LeigodCoreModel.PauseStatusId))
        {
            this.OnPropertyChanged(nameof(PauseStatusTextBrush));
        }

        if (e.PropertyName == nameof(LeigodCoreModel.MatchedGameProcess))
        {
            this.OnPropertyChanged(nameof(GameImageIcon));
        }
    }


    private async Task PollLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await UpdateTimer();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception e)
            {
                logger.LogError(e, "更新运行状态失败");
            }

            if (token.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await Task.Delay(1000, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task UpdateTimer()
    {
        _isRunning = Utils.IsRunning();
        if (!IsRunningText.Equals(RunningTextBlock.GetValue(TextBlock.TextProperty)))
        {
            OnPropertyChanged(nameof(IsRunningText));
        }

        OnPropertyChanged(nameof(RunningBtnVisible));
    }

    private void AdminBtn_OnClick(object sender, RoutedEventArgs e)
    {
    }

    private void InstallButton_OnClick(object sender, RoutedEventArgs e)
    {
        string serviceExe = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "LeigodPause.Service");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            serviceExe += ".exe";
        }

        InstallButton.IsEnabled = false;
        var isInstalled = Utils.IsInstalled();
        Task.Run(async () =>
        {
            Process? process = Process.Start(new ProcessStartInfo(serviceExe, isInstalled ? "--uninstall" : "--install")
            {
                Verb = "runas",
                UseShellExecute = true,
                CreateNoWindow = true,
            });
            process?.WaitForExit();
            int exitCode = process.ExitCode;
            if (exitCode == 0)
            {
                CancellationTokenSource tokenSource = new CancellationTokenSource();
                tokenSource.CancelAfter(5000);
                while (!tokenSource.IsCancellationRequested && (!isInstalled && !Utils.IsInstalled()) ||
                       (isInstalled && Utils.IsInstalled()))
                {
                    await Task.Delay(1, tokenSource.Token);
                }
            }

            await Dispatcher.InvokeAsync(() =>
            {
                InstallButton.IsEnabled = true;
                OnPropertyChanged(nameof(InstallButtonText));
                OnPropertyChanged(nameof(RunningBtnVisible));
                // this.InstallButton.SetCurrentValue(System.Windows.Controls.Button.ContentProperty, InstallButtonText);
            });
        });
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void RunButton_OnClick(object sender, RoutedEventArgs e)
    {
        Utils.RunService();
        _ = UpdateTimer();
    }

    // ===== WebView（原 WebPage） =====

    private void backBtn_Click(object sender, RoutedEventArgs e)
    {
        if (WebView?.CanGoBack == true)
        {
            WebView.GoBack();
        }
    }


    private void WebView_OnNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
    {
        UrlText.Text = e.Uri;
        ProgressBar.Visibility = Visibility.Visible;

        if (new Uri(e.Uri).Host == new Uri(leigodPersonalUrl).Host)
        {
            _ = WriteTokenToBrowser();
        }
    }

    private void WebView_OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        ProgressBar.Visibility = Visibility.Collapsed;
    }

    private void WebView_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        ProgressBar.Visibility = Visibility.Collapsed;
        if (e.InitializationException != null)
        {
            logger.LogError(e.InitializationException, "Webview初始化错误");
            throw e.InitializationException;
        }

        WebView.CoreWebView2.WindowCloseRequested += CoreWebView2_WindowCloseRequested;
        WebView.CoreWebView2.Settings.UserAgent = HttpClientExtensions.UserAgent;
        WebView.Source = new Uri(leigodPersonalUrl);
    }


    async Task WriteTokenToBrowser()
    {
        if (!string.IsNullOrEmpty(LeigodCoreModel.Token))
        {
            string command = $$"""
                               localStorage.setItem("account_token",JSON.stringify({"account_token":"{{LeigodCoreModel.Token}}"}))
                               """.Trim();
            var result = await WebView.ExecuteScriptAsync(command);
            logger.LogInformation("WriteTokenResult: {result}", result);
        }
    }

    private void UrlText_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            if (Uri.TryCreate(UrlText.Text, UriKind.RelativeOrAbsolute, out var uri))
            {
                WebView.Source = uri;
            }
        }
    }

    /// <summary>
    /// 仅在主窗口确认退出时由 MainView 调用：取消本页轮询、注销事件，并清理 WebView 缓存。
    /// 最小化到托盘不调用，因此不会误清缓存。
    /// </summary>
    private async Task CleanupForExitAsync()
    {
        _pollCts?.Cancel();
        LeigodCoreModel.PropertyChanged -= LeigodCoreModelOnPropertyChanged;
        if (WebView?.IsInitialized == true && WebView.CoreWebView2 != null)
        {
            await ClearCache();
        }
    }

    private async void CoreWebView2_WindowCloseRequested(object sender, object e)
    {
        if (WebView?.IsInitialized == true)
        {
            await ClearCache();
        }
    }


    private string[] whiteListDir = ["\\Local Storage\\"];

    // Clears autofill data.
    private async Task ClearCache()
    {
        CoreWebView2Profile profile;
        if (WebView.CoreWebView2 != null)
        {
            profile = WebView.CoreWebView2.Profile;

            CoreWebView2BrowsingDataKinds dataKinds =
                (CoreWebView2BrowsingDataKinds.GeneralAutofill |
                 CoreWebView2BrowsingDataKinds.DiskCache |
                 CoreWebView2BrowsingDataKinds.CacheStorage |
                 CoreWebView2BrowsingDataKinds.IndexedDb |
                 CoreWebView2BrowsingDataKinds.ServiceWorkers
                );
            await profile.ClearBrowsingDataAsync(dataKinds);
            try
            {
                var browerProcess = Process.GetProcessById((int)WebView.CoreWebView2.BrowserProcessId);
                WebView.Dispose();
                browerProcess.WaitForExit();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "浏览器进程未找到");
            }

            var dirName = Process.GetCurrentProcess().MainModule.FileName + ".WebView2";
            if (Directory.Exists(dirName))
            {
                var allFiles = Directory.GetFiles(dirName, "*", SearchOption.AllDirectories);
                var selectFiles = allFiles.Where(x => !whiteListDir.Any(x.Contains)).ToArray();
                foreach (var fileName in selectFiles)
                {
                    try
                    {
                        File.Delete(fileName);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, $"无法删除{Path.GetFileName(fileName)}");
                    }
                }

                var allDirs = Directory.GetDirectories(dirName, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Split('\\').Length);
                foreach (var folder in allDirs)
                {
                    if (Directory.GetFileSystemEntries(folder).Length == 0)
                    {
                        try
                        {
                            Directory.Delete(folder);
                        }
                        catch (Exception e)
                        {
                            logger.LogError(e, $"无法删除{Path.GetFileName(folder)}");
                        }
                    }
                }
            }
        }
    }

    private async Task PrintToken(bool showMsg = true)
    {
        try
        {
            // var result = (await WebView.ExecuteScriptAsync("localStorage.getItem('account_token')"))?.Trim('"');
            var token = (await WebView.ExecuteScriptAsync("JSON.parse(localStorage.getItem('account_token')).account_token"))?.Trim('"');
            if (!string.IsNullOrEmpty(token) && token != "null")
            {
                await Dispatcher.InvokeAsync(async () =>
                {
                    LeigodCoreModel.Token = token;
                    var messageBox = new MessageBox
                    {
                        Content = token,
                        CloseButtonText = "复制",
                        Title = "登录成功"
                    };
                    var boxResult = await messageBox.ShowDialogAsync();
                    Clipboard.SetDataObject(token);

                    //
                    // await LeigodCoreModel.RequestUserInfo()
                    //     .ContinueWith(t => this.Dispatcher.InvokeAsync(() => OnPropertyChanged(nameof(LeigodCoreModel))));
                });
            }
            else
            {
                await Dispatcher.InvokeAsync(async () =>
                {
                    var messageBox = new MessageBox
                    {
                        Content = "没有找到Token",
                        CloseButtonText = "关闭"
                    };
                    var boxResult = await messageBox.ShowDialogAsync();
                });
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "token提取错误");
        }
    }

    private async void TokenBtn_Click(object sender, RoutedEventArgs e)
    {
        await PrintToken();
    }

    private void PauseBtn_Click(object sender, RoutedEventArgs e)
    {
        _ = LeigodCoreModel.SendPauseState((i, s) =>
        {
            var messageBox = new MessageBox
            {
                Content = i + "\n" + s,
                Title = "暂停调用失败",
                Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
                CloseButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Danger
            };
            return messageBox.ShowDialogAsync();
        });
    }
}