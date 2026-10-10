// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.ComponentModel;
using LeigodPause.Models;
using LeigodPause.Utilities;
using LeigodPause.Views.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;

namespace LeigodPause.Views;

public partial class MainView
{
    public ObservableCollection<Wpf.Ui.Controls.MenuItem> TrayMenuItems { get; } =
    [
        new Wpf.Ui.Controls.MenuItem { Header = "Home", Tag = "tray_home" },
        new Wpf.Ui.Controls.MenuItem { Header = "Close", Tag = "tray_close" },
    ];

    public LeigodCoreModel LeigodCoreModel { get; } = App.Host.Services.GetRequiredService<LeigodCoreModel>();

    public MainView()
    {
        InitializeComponent();

        Loaded += (_, _) => RootNavigation.Navigate(typeof(DashboardPage));

        UiApplication.Current.MainWindow = this;

        this.ApplyTheme();
    }

    private void MainView_OnClosing(object sender, CancelEventArgs e)
    {
        TrayIcon.Dispose();
    }
}