using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LegodPause.Service;

public static class Utils
{
    public static void InstallService(string serviceName, string exePath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            InstallWindowsService(serviceName, exePath);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            InstallLinuxService(serviceName, exePath);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            InstallMacService(serviceName, exePath);
        }
    }

    private static void InstallWindowsService(string serviceName, string exePath)
    {
        try
        {
            var process = new Process();
            process.StartInfo.FileName = "sc.exe";
            process.StartInfo.Arguments = $"create {serviceName} binPath= \"{exePath}\"";
            process.StartInfo.Verb = "runas";
            process.StartInfo.UseShellExecute = true;
            process.Start();
            process.WaitForExit();
            Console.WriteLine("Windows 服务安装完成！");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Windows 服务安装失败: " + ex.Message);
        }
    }

    private static void InstallLinuxService(string serviceName, string exePath)
    {
        try
        {
            // 1. 创建 systemd service 文件内容
            string serviceContent = $@"
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
";
            string serviceFile = $"/etc/systemd/system/{serviceName}.service";

            // 2. 写入 systemd 服务文件（需 root 权限）
            System.IO.File.WriteAllText(serviceFile, serviceContent);

            // 3. 重新加载 systemd 并启动服务
            var process = new Process();
            process.StartInfo.FileName = "/bin/bash";
            process.StartInfo.Arguments = $"-c \"systemctl daemon-reload && systemctl enable {serviceName} && systemctl start {serviceName}\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            process.WaitForExit();

            Console.WriteLine("Linux 服务安装完成！");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Linux 服务安装失败: " + ex.Message);
        }
    }

    private static void InstallMacService(string serviceName, string exePath)
    {
        try
        {
            string plistContent = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
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
";
            string plistFile = $"/Library/LaunchDaemons/{serviceName}.plist";
            System.IO.File.WriteAllText(plistFile, plistContent);

            var process = new Process();
            process.StartInfo.FileName = "/bin/bash";
            process.StartInfo.Arguments = $"-c \"launchctl load {plistFile}\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            process.WaitForExit();

            Console.WriteLine("macOS 服务安装完成！");
        }
        catch (Exception ex)
        {
            Console.WriteLine("macOS 服务安装失败: " + ex.Message);
        }
    }
}