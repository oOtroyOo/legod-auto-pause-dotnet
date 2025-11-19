using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace LegodPause.Service;

public partial class Utils
{
    private static int InstallWindowsService(string serviceName, string exePath)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo("sc.exe", $"""
                                                                        create {serviceName} binPath= "{exePath}" start=auto displayname= "LegodPause 检查服务"  
                                                                        """)
            {
                Verb = "runas",
                WorkingDirectory = Environment.CurrentDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.GetEncoding("gbk"),
                StandardErrorEncoding = Encoding.GetEncoding("gbk")
            });

            process.WaitForExit();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            if (process.ExitCode == 0)
                Console.WriteLine($"Windows 服务安装完成！ out={output}");
            else
            {
                Console.WriteLine($"Windows 服务安装失败: {process.ExitCode} out={output}\n err={error}");
                return process.ExitCode;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Windows 服务安装失败: " + ex.Message);
            return -1;
        }

        return 0;
    }

    private static int UnInstallWindowsService(string serviceName)
    {
        try
        {
            if (IsInstalled())
            {
                var process = Process.Start(new ProcessStartInfo("sc.exe", $" stop {serviceName}")
                {
                    Verb = "runas",
                    WorkingDirectory = Environment.CurrentDirectory,
                    UseShellExecute = false,
                });

                process.WaitForExit();

                var process1 = Process.Start(new ProcessStartInfo("sc.exe", $" delete {serviceName}")
                {
                    Verb = "runas",
                    WorkingDirectory = Environment.CurrentDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.GetEncoding("gbk"),
                    StandardErrorEncoding = Encoding.GetEncoding("gbk")
                });

                process1.WaitForExit();
                var output = process1.StandardOutput.ReadToEnd();
                var error = process1.StandardError.ReadToEnd();
                Console.WriteLine($"服务卸载 code={process1.ExitCode} out={output}\n err={error}");
                return process1.ExitCode;
            }
            else
            {
                Console.WriteLine("服务未安装");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Windows 服务卸载失败: " + ex.Message);
        }

        return -1;
    }

    private static int RunWindowsService(string serviceName)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo("sc.exe", $" start {serviceName}")
            {
                Verb = "runas",
                WorkingDirectory = Environment.CurrentDirectory,
                UseShellExecute = true
            });

            process.WaitForExit();
            Console.WriteLine("Windows 服务启动完成！");
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Windows 服务启动失败: " + ex.Message);
        }

        return -1;
    }

    private static bool IsInstalledWindows(string serviceName)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo("sc.exe",
                    $" qc {serviceName}")
                {
                    // Verb = "runas",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    StandardOutputEncoding = Encoding.GetEncoding("gbk"),
                    StandardErrorEncoding = Encoding.GetEncoding("gbk"),
                }
            );

            process.WaitForExit();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            Console.WriteLine($"code={process.ExitCode} out={output}\n err={error}");
            return process.ExitCode == 0 && output?.Length > 0;
            /*
            SERVICE_NAME: LegodPauseService
            TYPE               : 10  WIN32_OWN_PROCESS
            STATE              : 1  STOPPED
            WIN32_EXIT_CODE    : 1077  (0x435)
            SERVICE_EXIT_CODE  : 0  (0x0)
            CHECKPOINT         : 0x0
            WAIT_HINT          : 0x0
            */
            /*
            SERVICE_NAME: LegodPauseService
            TYPE               : 10  WIN32_OWN_PROCESS
            START_TYPE         : 2   AUTO_START
            ERROR_CONTROL      : 1   NORMAL
            BINARY_PATH_NAME   : D:\oOtroyOo\legod-auto-pause-dotnet\dotnet\LegodPause.Service\bin\Debug\net481\LegodPause.Service.exe
            LOAD_ORDER_GROUP   :
            TAG                : 0
            DISPLAY_NAME       : LegodPause 检查服务
            DEPENDENCIES       :
            SERVICE_START_NAME : LocalSystem
            */
        }
        catch (Exception ex)
        {
            Console.WriteLine("运行失败: " + ex.Message);
        }

        return false;
    }

    private static bool IsRunningWindows(string serviceName)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo("sc.exe",
                    $" query {serviceName}")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    StandardOutputEncoding = Encoding.GetEncoding("gbk"),
                    StandardErrorEncoding = Encoding.GetEncoding("gbk"),
                }
            );

            process.WaitForExit();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            Console.WriteLine($"code={process.ExitCode} out={output} err={error}");
            
            // 检查服务状态是否为 RUNNING
            return process.ExitCode == 0 && output?.Contains("RUNNING") == true;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Windows服务状态检查失败: " + ex.Message);
        }

        return false;
    }
}