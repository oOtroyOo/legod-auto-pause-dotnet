using System.Runtime.CompilerServices;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using LeigodPause.Models;
using Wpf.Ui.Controls;
using Wpf.Ui.Tray.Controls;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace LeigodPause.Views;

public partial class MainView : INotifyPropertyChanged
{
    public LeigodCoreModel LeigodCoreModel { get; } = App.Host.Services.GetRequiredService<LeigodCoreModel>();
    private ILogger<MainView> logger = App.Host.Services.GetRequiredService<ILogger<MainView>>();
    private ServiceIpcClient _ipcClient = App.Host.Services.GetRequiredService<ServiceIpcClient>();
    private ConfigFile configFile = App.Host.Services.GetRequiredService<ConfigFile>();
    public MainViewCommand MainViewCommand { get; } = App.Host.Services.GetRequiredService<MainViewCommand>();

    public static readonly DependencyProperty GameImageIconProperty = DependencyProperty.Register(
        nameof(GameImageIcon), typeof(ImageSource), typeof(MainView), new PropertyMetadata(default(ImageSource)));

    public ImageSource GameImageIcon
    {
        get { return (ImageSource)GetValue(GameImageIconProperty); }
        set { SetValue(GameImageIconProperty, value); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;


    private CancellationTokenSource? _pollCts;

    // 为 true 时不再拦截关闭，用于托盘菜单的“退出/暂停并退出”真正结束进程。
    private bool _forceExit;

    private float _updateTime;


    // DashboardPage 注册的退出清理回调（如 WebView 缓存清理）；仅在确认退出时执行。
    private Func<Task>? _exitCleanup;

    // 托盘右键菜单项（保留 ItemsSource 以便后续扩展：新增项只需加一个带 Tag 的 MenuItem）。

    public MainView()
    {
        InitializeComponent();
        // 托盘菜单不在常规视觉树中，不会继承 MainView 的 DataContext，需手动指定绑定源
        TrayIcon.Menu.DataContext = this;
        UiApplication.Current.MainWindow = this;
        logger.LogInformation("games: {games}", configFile.GetValue<string>("games"));
        _updateTime = configFile.GetValue<float>("update");

        LeigodCoreModel.PropertyChanged += LeigodCoreModelOnPropertyChanged;
        this.ApplyTheme();
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void LeigodCoreModelOnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LeigodCoreModel.MatchedGameProcess))
        {
            MatchGameProcessToIcon();

            this.OnPropertyChanged(nameof(GameImageIcon));
        }
    }

    private void OnRootNavigationLoaded(object sender, RoutedEventArgs e)
    {
        RootNavigation.Navigate(typeof(DashboardPage));
    }

    private void MainView_OnActivated(object sender, EventArgs e)
    {
        // 先取消可能仍在运行的旧轮询循环，避免窗口多次激活/失焦产生多个并发循环。
        _pollCts?.Cancel();
        _pollCts = new CancellationTokenSource();
        _ = PollLoopAsync(_pollCts.Token);
    }

    private void MainView_OnDeactivated(object sender, EventArgs e)
    {
        _pollCts?.Cancel();
    }

    private void MainView_OnClosing(object sender, CancelEventArgs e)
    {
        if (_forceExit)
        {
            // 托盘菜单触发的退出：不再拦截，允许窗口真正关闭并结束进程。
            return;
        }

        // 点关闭按钮：隐藏窗口并驻留托盘，不退出进程。
        e.Cancel = true;
        _ = ClosingConfirm();
    }


    private void TitleBar_OnCloseClicked(TitleBar sender, RoutedEventArgs args)
    {
        // args.Handled = true;
        // CustomClosingMethod();
    }

    private async Task ClosingConfirm()
    {
        var messageBox = new MessageBox
        {
            Content = $"可关闭窗口最小化到托盘，Windows服务仍可继续运行",
            Title = "确认退出",
            // ----
            IsPrimaryButtonEnabled = LeigodCoreModel.IsNotPaused,
            PrimaryButtonText = "暂停并退出前端",
            PrimaryButtonAppearance = ControlAppearance.Caution,
            PrimaryButtonIcon = new SymbolIcon(SymbolRegular.PauseOff20) { FontSize = 20 },
            // ---- 
            SecondaryButtonText = "退出前端",
            SecondaryButtonAppearance = ControlAppearance.Success,
            SecondaryButtonIcon = new SymbolIcon(SymbolRegular.SignOut20) { FontSize = 20 },
            // ---- 
            CloseButtonText = "最小化到托盘",
            CloseButtonIcon = new SymbolIcon(SymbolRegular.PictureInPicture20) { FontSize = 20 },
            CloseButtonAppearance = ControlAppearance.Primary,
            // ---- 
            Width = 450,
        };
        var diag = await messageBox.ShowDialogAsync();
        switch (diag)
        {
            case MessageBoxResult.None:
                break;
            case MessageBoxResult.Primary:
                await LeigodCoreModel.SendPauseState();
                await ExitApplicationAsync();
                return;
            case MessageBoxResult.Secondary:
                await ExitApplicationAsync();
                return;
        }

        // 同时隐藏任务栏按钮，避免隐藏后任务栏残留失效条目。
        ShowInTaskbar = false;
        Hide();
    }


    public async Task PauseAndExitAsync()
    {
        // 先调用暂停指令，暂停失败则提示，正常则退出
        try
        {
            TaskCompletionSource<bool> faildTcs = new();
            var pauseTask = LeigodCoreModel.SendPauseState((i, s) =>
            {
                logger.LogWarning("暂停并退出：暂停调用返回错误 {code} {msg}", i, s);
                if (!IsVisible)
                {
                    ShowMainWindow(); // 先恢复主窗口，再弹提示
                }

                var messageBox = new MessageBox
                {
                    Content = $"暂停调用返回错误 {i} {s}",
                    Title = "暂停并退出",
                    CloseButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Caution
                };
                faildTcs.SetResult(true);
                return messageBox.ShowDialogAsync();
            });
            await Task.WhenAny(pauseTask, faildTcs.Task, Task.Delay(3000));

            if (faildTcs.Task.IsCompleted && faildTcs.Task.Result)
                return;

            await ExitApplicationAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "暂停并退出时暂停失败");
        }
    }

    /// <summary>
    /// DashboardPage 注册退出清理回调（如 WebView 缓存清理）。仅在确认退出时调用。
    /// </summary>
    public void RegisterExitCleanup(Func<Task> cleanup) => _exitCleanup = cleanup;

    private async Task RunExitCleanupAsync()
    {
        if (_exitCleanup != null)
        {
            try
            {
                await _exitCleanup();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "退出清理失败");
            }
        }
    }

    /// <summary>
    /// 退出应用：置强制退出标志 → 执行退出清理（WebView 缓存等）→ 关闭窗口。
    /// 确认框的“退出/暂停并退出”与托盘菜单“退出”都走这里，保证先清缓存再退出。
    /// </summary>
    public async Task ExitApplicationAsync()
    {
        _forceExit = true;
        await RunExitCleanupAsync();
        Close();
    }

    private void ShowMainWindow()
    {
        // 先恢复任务栏按钮（Hide 之后直接 Show 会导致任务栏按钮丢失），再显示并置前。
        ShowInTaskbar = true;
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Show();
        Activate();
    }


    private void MainView_OnLoaded(object sender, RoutedEventArgs e)
    {
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
            catch (Exception ex)
            {
                logger.LogError(ex, "周期性同步失败");
            }

            if (token.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await Task.Delay(100, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    DateTimeOffset lastInfoTime;

    private async Task UpdateTimer()
    {
        if (DateTimeOffset.Now - lastInfoTime > TimeSpan.FromSeconds(_updateTime))
        {
            _updateTime = configFile.GetValue<float>("update");
            if (_updateTime < 1) _updateTime = 1;

#if DEBUG
            // await _ipcClient.SendAsync(new IpcEnvelope() { Command = ServiceCommand.Ping })
            //     .ContinueWith(t =>
            //     {
            //         var respose = t.Result;
            //         logger.LogInformation("Ping response: {0}", respose);
            //     }, TaskContinuationOptions.OnlyOnRanToCompletion);
#endif


            if (LeigodCoreModel.Token is { Length: > 0 })
            {
                if (_ipcClient.IsConnected)
                {
                    var respose = await _ipcClient.SendAsync(new IpcEnvelope() { Command = ServiceCommand.RefreshUserInfo }, this._pollCts?.Token ?? CancellationToken.None);

                    if (respose != null)
                    {
                        logger.LogInformation("RefreshUserInfo response: {0}", respose);

                        if (respose.Code == 0)
                        {
                            if (respose.Payload is { Length: > 0 })
                            {
                                LeigodCoreModel.ApplyFields(JsonNode.Parse(Encoding.UTF8.GetString(respose.Payload)) as JsonObject);
                            }
                            // Code==0 但无载荷：合法的成功空响应，忽略
                        }
                        else
                        {
                            await HandleUserInfoFail(respose.Code, respose.Msg);
                        }
                    }
                }
                else
                {
                    await LeigodCoreModel.RequestUserInfo(HandleUserInfoFail).ConfigureAwait(false);
                }


                lastInfoTime = DateTimeOffset.Now;
            }
        }

        // FindGameProcess 不再由客户端发起：改由服务端主动下发，
        // 客户端接收泵收到后更新 LeigodCoreModel.MatchedGameProcess（无回执）。
        // 仅当服务未连接时回退为本地扫描，保证没装服务也能显示。
        if (!_ipcClient.IsConnected)
        {
            var loopTime = configFile.GetValue<float>("loop");
            if (loopTime < 1) loopTime = 1;
            if (DateTimeOffset.Now - LeigodCoreModel.MatchedGameProcessExitTime > TimeSpan.FromSeconds(loopTime))
            {
                await LeigodCoreModel.FindGameProcess().ConfigureAwait(false);
            }
        }
    }

    async Task HandleUserInfoFail(int code, string? msg)
    {
        // var messageBox = new MessageBox
        // {
        //     Content = $"{code}\n{msg}",
        //     Title = "用户登录失效",
        //     CloseButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Danger
        // };
        // await messageBox.ShowDialogAsync();
        // LeigodCoreModel.Token = null;
    }

    private void MatchGameProcessToIcon()
    {
        var str = LeigodCoreModel.MatchedGameProcess;
        var processPath = str?.Split('\n').LastOrDefault();

        // MatchedGameProcess 退化时可能只有进程名（无完整路径），ExtractAssociatedIcon 会抛异常，需容错
        if (!string.IsNullOrEmpty(processPath) && System.IO.File.Exists(processPath))
        {
            try
            {
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(processPath);
                if (icon == null) return;
                GameImageIcon = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                return;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "提取进程图标失败：{path}", processPath);
            }
        }

        if (GameImageIcon != null)
            GameImageIcon = null;
    }

    private void TrayIcon_OnLeftClick(NotifyIcon sender, RoutedEventArgs e)
    {
        // 左键托盘图标：恢复并显示主窗口。
        ShowMainWindow();
    }
}