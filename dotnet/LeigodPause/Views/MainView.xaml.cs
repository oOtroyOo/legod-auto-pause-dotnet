namespace LeigodPause.Views;

public partial class MainView
{
    public ObservableCollection<Wpf.Ui.Controls.MenuItem> TrayMenuItems { get; } =
    [
        new Wpf.Ui.Controls.MenuItem { Header = "Home", Tag = "tray_home" },
        new Wpf.Ui.Controls.MenuItem { Header = "Close", Tag = "tray_close" },
    ];

    public LeigodCoreModel LeigodCoreModel { get; } = App.Host.Services.GetRequiredService<LeigodCoreModel>();
    private ILogger<MainView> logger = App.Host.Services.GetRequiredService<ILogger<MainView>>();
    private ServiceIpcClient _ipcClient = App.Host.Services.GetRequiredService<ServiceIpcClient>();
    private CancellationTokenSource? _pollCts;

    public MainView()
    {
        InitializeComponent();

        Loaded += (_, _) => RootNavigation.Navigate(typeof(DashboardPage));

        UiApplication.Current.MainWindow = this;
        logger.LogInformation("games: {games}", ConfigFile.GetValue<string>("games"));
        this.ApplyTheme();
    }

    private void MainView_OnClosing(object sender, CancelEventArgs e)
    {
        _pollCts?.Cancel();
        TrayIcon.Dispose();
    }


    private void MainView_OnLoaded(object sender, RoutedEventArgs e)
    {
        // 去掉 Timer：改为任务完成后等待间隔再循环；UpdateTimer 内部已按 lastInfoTime 做 5s 节流。
        _pollCts = new CancellationTokenSource();
        _ = PollLoopAsync(_pollCts.Token);
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
    DateTimeOffset lastFindTime;

    private async Task UpdateTimer()
    {
        if (DateTimeOffset.Now - lastInfoTime > TimeSpan.FromSeconds(5))
        {
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

        if (_ipcClient.IsConnected)
        {
            // 节流：避免每 100ms 都打一次进程扫描 IPC，每 5s 才查一次。
            var now = DateTimeOffset.Now;
            if (now - lastFindTime > TimeSpan.FromSeconds(5))
            {
                lastFindTime = now;
                var respose = await _ipcClient.SendAsync(new IpcEnvelope() { Command = ServiceCommand.FindGameProcess }, this._pollCts?.Token ?? CancellationToken.None);

                if (respose != null)
                {
                    logger.LogInformation("RefreshUserInfo response: {0}", respose);

                    if (respose.Code == 0 && respose.Payload is { Length: > 0 })
                    {
                        LeigodCoreModel.MatchedGameProcess = Encoding.UTF8.GetString(respose.Payload);
                    }
                }
            }
        }
        else
        {
            await LeigodCoreModel.FindGameProcess().ConfigureAwait(false);
        }
    }

    async Task HandleUserInfoFail(int code, string? msg)
    {
        LeigodCoreModel.Token = null;
        var messageBox = new MessageBox
        {
            Content = $"{code}\n{msg}",
            Title = "用户登录失效",
            CloseButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Danger
        };
        await messageBox.ShowDialogAsync();
    }
}