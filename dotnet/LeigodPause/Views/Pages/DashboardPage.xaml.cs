// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using LeigodPause.Models;
using LeigodPause.Service;
using LeigodPause.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using Button = Wpf.Ui.Controls.Button;
using TextBlock = Wpf.Ui.Controls.TextBlock;
using Timer = System.Timers.Timer;

namespace LeigodPause.Views.Pages;

/// <summary>
/// Interaction logic for DashboardPage.xaml
/// </summary>
public partial class DashboardPage : INotifyPropertyChanged
{
    public string InstallButtonText => LeigodPause.Service.Utils.IsInstalled() ? "卸载服务" : "安装服务";
    private bool _isRunning = false;
    public string IsRunningText => IsRunning ? "运行中" : "未运行";
    public bool IsRunning => _isRunning;

    private int _counter = 0;

    private const string leigodLoginUrl = "https://www.leigod.com/mlogin";
    // "https://www.leigod.com/m/mlogin.html?region_code=1&language=zh_CN&platform=2";

    private const string leigodPersonalUrl = "https://www.leigod.com/personal-center";

    private const string UserAgent = "Mozilla/5.0 (Linux; Android 10; SM-G973F) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/110.0.0.0 Mobile Safari/537.36 EdgA/110.0.1587.63";
    public LeigodCoreModel LeigodCoreModel { get; } = App.Host.Services.GetRequiredService<LeigodCoreModel>();

    public DashboardPage()
    {
        DataContext = this;
        InitializeComponent();

        CounterTextBlock.SetCurrentValue(System.Windows.Controls.TextBlock.TextProperty, _counter.ToString());

        this.ApplyTheme();

        var currentApp = (App)App.CurrentApp;
        var appMainWindow = currentApp.MainWindow;
        appMainWindow.Closing += AppMainWindowOnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var timer = new Timer(1000);
        timer.Elapsed += (s, e) => { this.Dispatcher.Invoke(UpdateTimer); };
        timer.Start();
        UpdateTimer();
    }

    void UpdateTimer()
    {
        _isRunning = LeigodPause.Service.Utils.IsRunning();
        if (!IsRunningText.Equals(this.RunningTextBlock.GetValue(TextBlock.TextProperty)))
        {
            this.OnPropertyChanged(nameof(IsRunningText));
        }

        this.RunButton.Visibility = (LeigodPause.Service.Utils.IsInstalled() && !IsRunning) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnBaseButtonClick(object sender, RoutedEventArgs e)
    {
        CounterTextBlock.SetCurrentValue(
            System.Windows.Controls.TextBlock.TextProperty,
            (++_counter).ToString()
        );
    }

    private void AdminBtn_OnClick(object sender, RoutedEventArgs e)
    {
    }

    private void InstallButton_OnClick(object sender, RoutedEventArgs e)
    {
        string serviceExe = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "LeigodPause.Service");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            serviceExe += ".exe";
        }

        this.InstallButton.IsEnabled = false;
        var isInstalled = LeigodPause.Service.Utils.IsInstalled();
        Task.Run(async () =>
        {
            Process? process = Process.Start(new ProcessStartInfo(serviceExe, isInstalled ? "--uninstall" : "--install")
            {
                Verb = "runas",
                UseShellExecute = true,
                CreateNoWindow = true,
            });
            process?.WaitForExit();
            int exitCode = process.ExitCode;
            if (exitCode == 0)
            {
                CancellationTokenSource tokenSource = new CancellationTokenSource();
                tokenSource.CancelAfter(5000);
                while (!tokenSource.IsCancellationRequested && (!isInstalled && !LeigodPause.Service.Utils.IsInstalled()) || (isInstalled && LeigodPause.Service.Utils.IsInstalled()))
                {
                    await Task.Delay(1, tokenSource.Token);
                }
            }
            else
            {
            }

            await this.Dispatcher.InvokeAsync(() =>
            {
                this.InstallButton.IsEnabled = true;
                this.OnPropertyChanged(nameof(InstallButtonText));
                // this.InstallButton.SetCurrentValue(System.Windows.Controls.Button.ContentProperty, InstallButtonText);
            });
        });
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void RunButton_OnClick(object sender, RoutedEventArgs e)
    {
        Utils.RunService();
        UpdateTimer();
    }

    // ===== WebView（原 WebPage） =====

    private void backBtn_Click(object sender, RoutedEventArgs e)
    {
        if (WebView?.CanGoBack == true)
        {
            WebView.GoBack();
        }
    }


    private void WebView_OnNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
    {
        UrlText.Text = e.Uri;
        ProgressBar.Visibility = Visibility.Visible;
    }

    private void WebView_OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        ProgressBar.Visibility = Visibility.Collapsed;
    }

    private void WebView_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        ProgressBar.Visibility = Visibility.Collapsed;
        if (e.InitializationException != null)
        {
            Console.WriteLine(e.InitializationException);
            throw e.InitializationException;
        }

        WebView.CoreWebView2.WindowCloseRequested += CoreWebView2_WindowCloseRequested;
        WebView.CoreWebView2.Settings.UserAgent = UserAgent;
        WebView.Source = new Uri(leigodPersonalUrl);
        // WebView.Source = new Uri(leigodLoginUrl);
    }

    private void UrlText_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            if (Uri.TryCreate(UrlText.Text, UriKind.RelativeOrAbsolute, out var uri))
            {
                WebView.Source = uri;
            }
        }
    }

    private bool _isCleaning;

    private async void AppMainWindowOnClosing(object sender, CancelEventArgs e)
    {
        var appWindow = ((Window)sender);
        if (WebView?.IsInitialized == true)
        {
            if (!_isCleaning)
            {
                e.Cancel = true;
                _isCleaning = true;
                appWindow.ShowInTaskbar = false;
                appWindow.Hide();
                if (WebView.CoreWebView2 != null)
                {
                    await ClearCache();
                }

                // 清理完成，关闭窗口

                appWindow.Close();
            }
        }
    }

    private async void CoreWebView2_WindowCloseRequested(object sender, object e)
    {
        if (WebView?.IsInitialized == true)
        {
            await ClearCache();
        }
    }


    private string[] whiteListDir = ["\\Local Storage\\"];

    // Clears autofill data.
    private async Task ClearCache()
    {
        CoreWebView2Profile profile;
        if (WebView.CoreWebView2 != null)
        {
            profile = WebView.CoreWebView2.Profile;

            CoreWebView2BrowsingDataKinds dataKinds =
                (CoreWebView2BrowsingDataKinds.GeneralAutofill |
                 CoreWebView2BrowsingDataKinds.DiskCache |
                 CoreWebView2BrowsingDataKinds.CacheStorage |
                 CoreWebView2BrowsingDataKinds.IndexedDb |
                 CoreWebView2BrowsingDataKinds.ServiceWorkers
                );
            await profile.ClearBrowsingDataAsync(dataKinds);
            try
            {
                var browerProcess = Process.GetProcessById((int)WebView.CoreWebView2.BrowserProcessId);
                WebView.Dispose();
                browerProcess.WaitForExit();
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }

            var dirName = Process.GetCurrentProcess().MainModule.FileName + ".WebView2";
            if (Directory.Exists(dirName))
            {
                var allFiles = Directory.GetFiles(dirName, "*", SearchOption.AllDirectories);
                var selectFiles = allFiles.Where(x => !whiteListDir.Any(x.Contains)).ToArray();
                foreach (var fileName in selectFiles)
                {
                    try
                    {
                        File.Delete(fileName);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }

                var allDirs = Directory.GetDirectories(dirName, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Split('\\').Length);
                foreach (var folder in allDirs)
                {
                    if (Directory.GetFileSystemEntries(folder).Length == 0)
                    {
                        try
                        {
                            Directory.Delete(folder);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                    }
                }
            }
        }
    }

    private async Task PrintToken(bool showMsg = true)
    {
        try
        {
            var result = (await WebView.ExecuteScriptAsync("localStorage.getItem('account_token')"))?.Trim('"');
            var token = (await WebView.ExecuteScriptAsync("JSON.parse(localStorage.getItem('account_token')).account_token"))?.Trim('"');
            if (!string.IsNullOrEmpty(token) && token != "null")
            {
                LeigodCoreModel.Token = token;
                await this.Dispatcher.InvokeAsync(async () =>
                {
                    var messageBox = new Wpf.Ui.Controls.MessageBox
                    {
                        Content = token,
                        CloseButtonText = "复制",
                        Title = "登录成功"
                    };
                    var boxResult = await messageBox.ShowDialogAsync();
                    Clipboard.SetDataObject(token);
                });
            }
            else
            {
                await this.Dispatcher.InvokeAsync(async () =>
                {
                    var messageBox = new Wpf.Ui.Controls.MessageBox
                    {
                        Content = "没有找到Token",
                        CloseButtonText = "关闭"
                    };
                    var boxResult = await messageBox.ShowDialogAsync();
                });
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private async void backTokenBtn_Click(object sender, RoutedEventArgs e)
    {
        await PrintToken();
    }
}
