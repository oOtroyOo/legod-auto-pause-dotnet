using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LeigodPause.Views;
using Microsoft.Extensions.Hosting;

namespace LeigodPause;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    [DllImport("Kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    [DllImport("kernel32.dll")]
    public static extern uint GetOEMCP();

    public static Encoding GetOEMEncoding()
    {
        uint oemCP = GetOEMCP();
        return Encoding.GetEncoding((int)oemCP);
    }

    public static App CurrentApp => (App)Current;

    /// <summary>
    /// 把致命异常写入 leigod-crash.log，方便排查“双击无反应”类问题。
    /// </summary>
    internal static void LogFatal(Exception? ex, string where)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "log/leigod-crash.log");
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {where}:\n{ex}\n\n");
        }
        catch
        {
        }
    }


    public static readonly IHost Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
        .ConfigureServices((context, services) =>
            services
                .AddLogging(loggingBuilder =>
                {
                    loggingBuilder.AddConsole();
#if DEBUG
                    loggingBuilder.AddProvider(new FileLoggerProvider());
#endif
                })
                .AddLeigodHttpClient()
                .AddSingleton<MainView>()
                .AddSingleton<ConfigFile>()
                .AddSingleton<LeigodCoreModel>()
                .AddSingleton<ServiceIpcClient>()
                .AddSingleton<ProtobufMsgEncoder>()
        ).Build();

    /// <summary>
    /// Occurs when the application is loading.
    /// </summary>
    private void OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
#if !NETFRAMEWORK
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
            AttachConsole(-1);

            // 跟随 Windows 系统主题（深色/浅色）启动，之后仍可用设置页手动切换
            SystemTheme systemTheme = Wpf.Ui.Appearance.ApplicationThemeManager.GetSystemTheme();
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(systemTheme == SystemTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light);
            // Wpf.Ui.Appearance.ApplicationThemeManager.Apply(GetWindowsTheme());

            var ipcClient = Host.Services.GetRequiredService<ServiceIpcClient>();
            Task.Run(ipcClient.Connect);

            var  configFile = Host.Services.GetRequiredService<ConfigFile>();
            configFile.OnSave += () => { _ = ipcClient.SendAsync(new IpcEnvelope() { Command = ServiceCommand.ReloadConfig }); };
        }
        catch (Exception ex)
        {
            LogFatal(ex, "OnStartup");
            System.Windows.MessageBox.Show(ex.ToString(), "LeigodPause 启动失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    /// <summary>
    /// 读取 Windows “应用”模式主题：AppsUseLightTheme 为 0 表示深色，1 表示浅色。
    /// </summary>
    private static Wpf.Ui.Appearance.ApplicationTheme GetWindowsTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            // AppsUseLightTheme: 0 = 深色, 1 = 浅色
            var value = key?.GetValue("AppsUseLightTheme");
            return value is 0
                ? Wpf.Ui.Appearance.ApplicationTheme.Dark
                : Wpf.Ui.Appearance.ApplicationTheme.Light;
        }
        catch
        {
            return Wpf.Ui.Appearance.ApplicationTheme.Light;
        }
    }

    /// <summary>
    /// Occurs when the application is closing.
    /// </summary>
    private void OnExit(object sender, ExitEventArgs e)
    {
    }


    /// <summary>
    ///  使用UAC管理员盾牌图标
    /// </summary>
    public Lazy<ImageSource> UacImageSource { get; } = new Lazy<ImageSource>(() =>
        {
            // System.Windows.Forms.SystemInformation.SmallIconSize.Width
            // (int) System.Windows.SystemParameters.SmallIconWidth
            var image = ShellExtension.LoadImage(IntPtr.Zero, "#106", 1, (int)SystemParameters.SmallIconWidth,
                (int)SystemParameters.SmallIconHeight, 0);
            var _uacImageSource = Imaging.CreateBitmapSourceFromHIcon(image, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            return _uacImageSource;
        }
    );


    public Lazy<ImageSource> CurrentExeIcon { get; } = new Lazy<ImageSource>(() =>
    {
        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
        using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath))
        {
            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            return source;
        }
    });

    /// <summary>
    /// Occurs when an exception is thrown by an application but not handled.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogFatal(e.Exception, "DispatcherUnhandledException");
        System.Windows.MessageBox.Show(e.Exception.ToString(), "LeigodPause 未处理异常", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        e.Handled = true;
    }
}