using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.Win32;

namespace LegodPause;
public class ShellExtension
{
    [DllImport("user32.dll")]
    public static extern int SendMessage(int hWnd, uint msg, int wParam, int lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr LoadImage(
        IntPtr hinst,
        string lpszName,
        uint uType,
        int cxDesired,
        int cyDesired,
        uint fuLoad);

    [DllImport("shell32.dll")]
    public static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    public static Icon LoadShellIcon(string dllPath, int iconIndex)
    {
        IntPtr hIcon = ExtractIcon(IntPtr.Zero, dllPath, iconIndex);
        if (hIcon != IntPtr.Zero)
        {
            return Icon.FromHandle(hIcon);
        }

        return null;
    }



  
    public static void RestartExplorer()
    {
        // 查找并终止 Explorer 进程
        var explorerProcesses = Process.GetProcessesByName("explorer");
        foreach (var process in explorerProcesses)
        {
            process.Kill();
        }

        // // 启动新的 Explorer 进程
        var startInfo = new ProcessStartInfo("explorer.exe")
        {
            UseShellExecute = true
        };

        Process.Start(startInfo)?.Kill();
        // 通知系统重新加载任务栏和桌面
        // var thread = new Thread(() => { SendMessage(HWND_BROADCAST, WM_SETTINGCHANGE, 0, 0); });
        // thread.Start();
        // thread.Abort();
    }
    
}