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
        TrayIcon.Dispose();
    }


    private void MainView_OnLoaded(object sender, RoutedEventArgs e)
    {
        var timer = new Timer(100);
        timer.Elapsed += (s, e) => { Dispatcher.Invoke(UpdateTimer); };
        timer.Start();
    }

    DateTimeOffset lastInfoTime;

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
                // await LeigodCoreModel.RequestUserInfo((i, s) =>
                // {
                //     LeigodCoreModel.Token = null;
                //     var messageBox = new Wpf.Ui.Controls.MessageBox
                //     {
                //         Content = i + "\n" + s,
                //         Title = "用户登录失效",
                //         CloseButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Danger
                //     };
                //     messageBox.ShowDialogAsync();
                // }).ConfigureAwait(false);
                var respose = await _ipcClient.SendAsync(new IpcEnvelope() { Command = ServiceCommand.RefreshUserInfo });

                logger.LogInformation("RefreshUserInfo response: {0}", respose);

                if (respose is { Code: 0, Payload.Length: > 0 })
                {
                    LeigodCoreModel.ApplyFields(JsonNode.Parse(Encoding.UTF8.GetString(respose.Payload)) as JsonObject);
                }
                else
                {
                    LeigodCoreModel.Token = null;
                    var messageBox = new MessageBox
                    {
                        Content = respose.Code + "\n" + respose.Msg,
                        Title = "用户登录失效",
                        CloseButtonAppearance = Wpf.Ui.Controls.ControlAppearance.Danger
                    };
                    await messageBox.ShowDialogAsync();
                }


                lastInfoTime = DateTimeOffset.Now;
            }
        }
    }
}