using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace LegodPause.Service;

public partial class Utils
{
    private static int InstallMacService(string serviceName, string exePath)
    {
        try
        {
            string plistContent = $"""
                                   <?xml version="1.0" encoding="UTF-8"?>
                                                                     
                                   <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                                   <plist version="1.0">
                                   <dict>
                                       <key>Label</key>
                                       <string>{serviceName}</string>
                                       <key>ProgramArguments</key>
                                       <array>
                                           <string>{exePath}</string>
                                       </array>
                                       <key>RunAtLoad</key>
                                       <true/>
                                       <key>KeepAlive</key>
                                       <true/>
                                   </dict>
                                   </plist>
                                   """;
            string plistFile = $"/Library/LaunchDaemons/{serviceName}.plist";
            System.IO.File.WriteAllText(plistFile, plistContent);

            var process = Process.Start(new ProcessStartInfo("/bin/bash", $$"""
                                                                            -c "launchctl load {plistFile}"
                                                                            """)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true
            });

            process.WaitForExit();

            Console.WriteLine("macOS 服务安装完成！");
        }
        catch (Exception ex)
        {
            Console.WriteLine("macOS 服务安装失败: " + ex.Message);
            return -1;
        }


        return 0;
    }

    private static int UnInstallMacService(string serviceName)
    {
        try
        {
            // 1. Stop the service
            var stopProcess = Process.Start(new ProcessStartInfo("launchctl", $"unload -w /Library/LaunchDaemons/{serviceName}.plist")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true
            });
            stopProcess.WaitForExit();

            // 2. Remove the plist file
            string plistFile = $"/Library/LaunchDaemons/{serviceName}.plist";
            if (File.Exists(plistFile))
            {
                File.Delete(plistFile);
            }

            Console.WriteLine("macOS 服务卸载完成！");
            return stopProcess.ExitCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine("macOS 服务卸载失败: " + ex.Message);
        }

        return -1;
    }

    private static int RunMacService(string serviceName)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo("launchctl", $"load -w /Library/LaunchDaemons/{serviceName}.plist")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true
            });

            process.WaitForExit();
            Console.WriteLine("macOS 服务启动完成！");
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine("macOS 服务启动失败: " + ex.Message);
        }

        return -1;
    }

    private static bool IsInstalledMac(string serviceName)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo("/bin/bash", $"""
                                                                           -c "launchctl list | grep -q {serviceName}"
                                                                           """)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true
            });

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("macOS服务检查失败: " + ex.Message);
            return false;
        }
    }

    private static bool IsRunningMac(string serviceName)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo("/bin/bash", $"""
                                                                           -c "launchctl list | grep {serviceName} | head -1'"
                                                                           """)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            process.WaitForExit();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            Console.WriteLine($"code={process.ExitCode} out={output} err={error}");
            
            // 如果输出包含PID（数字），说明服务正在运行
            // 如果输出包含"-"，说明服务已加载但未运行
            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) && output.Trim() != "-" && int.TryParse(output.Trim(), out _);
        }
        catch (Exception ex)
        {
            Console.WriteLine("macOS服务状态检查失败: " + ex.Message);
        }

        return false;
    }
}