using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace LegodPause.Core;

public partial class PlatformUtils
{
    public const string MainServiceName = "LegodPauseService";

    public static int InstallService(string serviceName = MainServiceName, string? exePath = null)
    {
        exePath ??= Process.GetCurrentProcess().MainModule.FileName;
        int exitCode = -1;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            exitCode = InstallWindowsService(serviceName, exePath);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            exitCode = InstallLinuxService(serviceName, exePath);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            exitCode = InstallMacService(serviceName, exePath);
        }

        if (IsInstalled())
        {
            RunService(serviceName);
        }

        return exitCode;
    }


    public static int UnInstallService(string serviceName = MainServiceName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return UnInstallWindowsService(serviceName);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return UnInstallLinuxService(serviceName);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return UnInstallMacService(serviceName);
        }

        return -2;
    }


    public static int RunService(string serviceName = MainServiceName)
    {
        Console.WriteLine("启动服务 " + serviceName);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return RunWindowsService(serviceName);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return RunLinuxService(serviceName);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RunMacService(serviceName);
        }

        return -1;
    }


    public static bool IsInstalled(string serviceName = MainServiceName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return IsInstalledWindows(serviceName);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return IsInstalledLinux(serviceName);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return IsInstalledMac(serviceName);
        }

        return false;
    }

    public static bool IsRunning(string serviceName = MainServiceName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return IsRunningWindows(serviceName);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return IsRunningLinux(serviceName);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return IsRunningMac(serviceName);
        }

        return false;
    }
}