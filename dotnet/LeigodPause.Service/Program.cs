using System.Runtime.InteropServices;
using LeigodPause.Service.Api;

namespace LeigodPause.Service;

public class Program
{
    [DllImport("Kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    public static IHost? AppHost;

    public static int Main(string[] args)
    {
        AttachConsole(-1);
#if NETCOREAPP
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
#endif

        if (Array.IndexOf(args, "-i") > -1 || Array.IndexOf(args, "--install") > -1)
        {
            return Utils.InstallService();
        }

        if (Array.IndexOf(args, "-u") > -1 || Array.IndexOf(args, "--uninstall") > -1)
        {
            return Utils.UnInstallService();
        }

        BuildService(args)
            .Run();
#if NETFRAMEWORK
#endif
        Console.ReadKey();
        return 0;
    }

    private static IHost BuildService(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services
            .AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole();
                loggingBuilder.AddProvider(new FileLoggerProvider(GetLogFilePath()));
            })
            .AddHostedService<Worker>()
            .AddLeigodHttpClient()
            .AddWindowsService();
        AppHost = builder.Build();
        return AppHost;
    }

    // 每次启动生成一个带时间戳的日志文件：<exe目录>/log/yyyyMMdd-HHmmss.txt
    private static string GetLogFilePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "log", $"{DateTime.Now:yyyyMMdd-HHmmss}.txt");
    }
}