using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace LegodPause.Service;

public class Program
{
    [DllImport("Kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    public static int Main(string[] args)
    {
        Console.WriteLine("dir=" + Environment.CurrentDirectory);
#if NETCOREAPP
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif

        if (Array.IndexOf(args, "-i") > -1 || Array.IndexOf(args, "--install") > -1)
        {
            return Utils.InstallService();
        }

        if (Array.IndexOf(args, "-u") > -1 || Array.IndexOf(args, "--uninstall") > -1)
        {
            return Utils.UnInstallService();
        }

        BuildService(args);


#if NETFRAMEWORK

#endif
        Console.ReadKey();
        return 0;
    }

    private static void BuildService(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddHostedService<Worker>();
        builder.Services.AddWindowsService();
        var host = builder.Build();
        host.Run();
    }
}