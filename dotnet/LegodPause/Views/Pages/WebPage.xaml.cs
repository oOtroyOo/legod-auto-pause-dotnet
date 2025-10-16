using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using LegodPause;
using Microsoft.Web.WebView2.Core;
using Wpf.Ui.Controls;

namespace LegodPause.Views.Pages;

public partial class WebPage
{
    private const string leigodLoginUrl = "https://www.leigod.com/m/mlogin.html?region_code=1&language=zh_CN&platform=2";

    private const string UserAgent = "Mozilla/5.0 (Linux; Android 10; SM-G973F) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/110.0.0.0 Mobile Safari/537.36 EdgA/110.0.1587.63";

    public WebPage()
    {
        // CoreWebView2Environment.CreateAsync
        // var opt = (options ?? new CoreWebView2EnvironmentOptions())._nativeICoreWebView2EnvironmentOptions
        InitializeComponent();
        var currentApp = (App)App.CurrentApp;
        var appMainWindow = currentApp.MainWindow;
        appMainWindow.Closing += AppMainWindowOnClosing;
    }


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
        WebView.Source = new Uri(leigodLoginUrl);
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
                    await ClearAutofillData();
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
            await ClearAutofillData();
        }
    }

    // Clears autofill data.
    private async Task ClearAutofillData()
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
            Console.WriteLine();
        }
    }
}