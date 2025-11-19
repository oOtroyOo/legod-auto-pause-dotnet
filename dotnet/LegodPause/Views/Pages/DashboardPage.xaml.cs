// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Controls;
using LegodPause.Service;
using LegodPause.Utilities;
using Button = Wpf.Ui.Controls.Button;
using TextBlock = Wpf.Ui.Controls.TextBlock;
using Timer = System.Timers.Timer;

namespace LegodPause.Views.Pages;

/// <summary>
/// Interaction logic for DashboardPage.xaml
/// </summary>
public partial class DashboardPage : INotifyPropertyChanged
{
    public string InstallButtonText => LegodPause.Service.Utils.IsInstalled() ? "卸载服务" : "安装服务";
    private bool _isRunning = false;
    public string IsRunningText => IsRunning ? "运行中" : "未运行";
    public bool IsRunning => _isRunning;

    private int _counter = 0;

    public DashboardPage()
    {
        DataContext = this;
        InitializeComponent();

        CounterTextBlock.SetCurrentValue(System.Windows.Controls.TextBlock.TextProperty, _counter.ToString());

        this.ApplyTheme();
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
        _isRunning = LegodPause.Service.Utils.IsRunning();
        if (!IsRunningText.Equals(this.RunningTextBlock.GetValue(TextBlock.TextProperty)))
        {
            this.OnPropertyChanged(nameof(IsRunningText));
        }

        this.RunButton.Visibility = (LegodPause.Service.Utils.IsInstalled() && !IsRunning) ? Visibility.Visible : Visibility.Collapsed;
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
        string serviceExe = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "LegodPause.Service");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            serviceExe += ".exe";
        }

        this.InstallButton.IsEnabled = false;
        var isInstalled = LegodPause.Service.Utils.IsInstalled();
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
                while (!tokenSource.IsCancellationRequested && (!isInstalled && !LegodPause.Service.Utils.IsInstalled()) || (isInstalled && LegodPause.Service.Utils.IsInstalled()))
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
}