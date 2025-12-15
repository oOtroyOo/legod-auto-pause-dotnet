using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace LegodPause.Core;

public partial class PlatformUtils
{
    private static int InstallLinuxService(string serviceName, string exePath)
    {
        try
        {
            // 1. 创建 systemd service 文件内容
            string serviceContent = $"""
                                     [Unit]
                                     Description={serviceName}
                                     After=network.target

                                     [Service]
                                     Type=simple
                                     ExecStart={exePath}
                                     Restart=always
                                     User=root

                                     [Install]
                                     WantedBy=multi-user.target
                                     """;
            string serviceFile = $"/etc/systemd/system/{serviceName}.service";

            // 2. 写入 systemd 服务文件（需 root 权限）
            System.IO.File.WriteAllText(serviceFile, serviceContent);

            // 3. 重新加载 systemd 并启动服务
            var process = Process.Start(new ProcessStartInfo("/bin/bash", $"""
                                                                            -c "systemctl daemon-reload && systemctl enable {serviceName} && systemctl start {serviceName}" 
                                                                           """)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true
            });

            process.WaitForExit();

            Console.WriteLine("Linux 服务安装完成！");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Linux 服务安装失败: " + ex.Message);
            return -1;
        }

        return 0;
    }

    private static int UnInstallLinuxService(string serviceName)
    {
        try
        {
            // 3. 重新加载 systemd 并启动服务
            var process = Process.Start(new ProcessStartInfo("/bin/bash", $"""
                                                                           -c "systemctl stop {serviceName}" 
                                                                           """)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true
            });

            string serviceFile = $"/etc/systemd/system/{serviceName}.service";

            if (File.Exists(serviceFile))
                System.IO.File.Delete(serviceFile);

            // 3. 重新加载 systemd 并启动服务
            var process1 = Process.Start(new ProcessStartInfo("/bin/bash", $"""
                                                                            -c "systemctl daemon-reload"
                                                                            """)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true
            });

            process1.WaitForExit();

            Console.WriteLine("Linux 服务安装完成！");

            return process1.ExitCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Linux 服务安装失败: " + ex.Message);
            return -1;
        }

        return -1;
    }

    private static int RunLinuxService(string serviceName)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo("/bin/bash", $"""
                                                                           -c "systemctl start {serviceName}"
                                                                           """)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true
            });

            process.WaitForExit();
            Console.WriteLine("Linux 服务启动完成！");
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Linux 服务启动失败: " + ex.Message);
            return -1;
        }
    }

    private static bool IsInstalledLinux(string serviceName)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo("/bin/bash", $"""
                                                                           -c "systemctl list-unit-files | grep -q {serviceName}.service"
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
            Console.WriteLine("Linux服务检查失败: " + ex.Message);
            return false;
        }
    }

    private static bool IsRunningLinux(string serviceName)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo("/bin/bash", $"""
                                                                           -c "systemctl is-active {serviceName}"
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
            
            // systemctl is-active 返回 0 表示服务正在运行
            return process.ExitCode == 0 && output?.Trim() == "active";
        }
        catch (Exception ex)
        {
            Console.WriteLine("Linux服务状态检查失败: " + ex.Message);
        }

        return false;
    }
}