using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using LegodPause.Core;
using LegodPause.Core.Network;
using LegodPause.Service.Proto;

namespace LegodPause.Service;

public class Program
{
    [DllImport("Kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    public static IHost? AppHost;

    public static int Main(string[] args)
    {
        AttachConsole(-1);
#if NETCOREAPP
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif

        if (Array.IndexOf(args, "-i") > -1 || Array.IndexOf(args, "--install") > -1)
        {
            return PlatformUtils.InstallService();
        }

        if (Array.IndexOf(args, "-u") > -1 || Array.IndexOf(args, "--uninstall") > -1)
        {
            return PlatformUtils.UnInstallService();
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
        builder.Services.AddHostedService<Worker>();
        builder.Services.AddLegodPuseCore();
        builder.Services.AddSingleton<NetworkServer>();
        builder.Services.AddWindowsService();
        builder.Services.AddHttpClient();
        AppHost = builder.Build();
        return AppHost;
    }
}