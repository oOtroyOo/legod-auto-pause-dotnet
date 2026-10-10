// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LeigodPause.Models;
using LeigodPause.Service;
using LeigodPause.Views;
using LeigodPause.Views.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LeigodPause;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    [DllImport("Kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    [DllImport("kernel32.dll")]
    public static extern uint GetOEMCP();

    public static Encoding GetOEMEncoding()
    {
        uint oemCP = GetOEMCP();
        return Encoding.GetEncoding((int)oemCP);
    }

    public static App CurrentApp => (App)Current;


    public static readonly IHost Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
        .ConfigureServices((context, services) =>
        {
            services.AddHttpClient("leigod", client =>
            {
                client.DefaultRequestHeaders.UserAgent.TryParseAdd(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.96 Safari/537.36 Edg/88.0.705.53");
                client.DefaultRequestHeaders.Accept.TryParseAdd("application/json, text/javascript, */*; q=0.01");
                client.DefaultRequestHeaders.AcceptEncoding.TryParseAdd("gzip, deflate, br");
                client.DefaultRequestHeaders.AcceptLanguage.TryParseAdd("zh-CN,zh;q=0.9,en;q=0.8,en-GB;q=0.7,en-US;q=0.6");
                client.DefaultRequestHeaders.Add("DNT", "1");
                client.DefaultRequestHeaders.Referrer = new Uri("https://www.leigod.com/");
                client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "empty");
                client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");
                client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-site");
            });
            services.AddSingleton<MainView>();
            services.AddSingleton<LeigodCoreModel>();
        }).Build();

    /// <summary>
    /// Occurs when the application is loading.
    /// </summary>
    private void OnStartup(object sender, StartupEventArgs e)
    {
#if !NETFRAMEWORK
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
        Console.WriteLine("欢迎");
        AttachConsole(-1);
    }

    /// <summary>
    /// Occurs when the application is closing.
    /// </summary>
    private void OnExit(object sender, ExitEventArgs e)
    {
    }


    /// <summary>
    ///  使用UAC管理员盾牌图标
    /// </summary>
    public Lazy<ImageSource> UacImageSource { get; } = new Lazy<ImageSource>(() =>
        {
            // System.Windows.Forms.SystemInformation.SmallIconSize.Width
            // (int) System.Windows.SystemParameters.SmallIconWidth
            var image = ShellExtension.LoadImage(IntPtr.Zero, "#106", 1, (int)SystemParameters.SmallIconWidth,
                (int)SystemParameters.SmallIconHeight, 0);
            var _uacImageSource = Imaging.CreateBitmapSourceFromHIcon(image, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            return _uacImageSource;
        }
    );


    public Lazy<ImageSource> CurrentExeIcon { get; } = new Lazy<ImageSource>(() =>
    {
        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
        using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath))
        {
            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            return source;
        }
    });

    /// <summary>
    /// Occurs when an exception is thrown by an application but not handled.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // For more info see https://docs.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=windowsdesktop-6.0
    }
}