using Microsoft.Extensions.Hosting.WindowsServices;

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
// #if DEBUG
                loggingBuilder.AddProvider(new FileLoggerProvider());
// #endif
            })
            .AddLeigodHttpClient()
            .AddSingleton<LeigodCoreModel>()
            .AddSingleton<ServiceIpcServer>()
            .AddSingleton<ProtobufMsgEncoder>()
            .AddSingleton<ConfigFile>()
            .AddHostedService<Worker>();

        if (WindowsServiceHelpers.IsWindowsService())
        {
            builder.Services
                .AddWindowsService()
                .AddSingleton<IHostLifetime, CustomServiceLifetime>();
        }

        AppHost = builder.Build();
        return AppHost;
    }
}