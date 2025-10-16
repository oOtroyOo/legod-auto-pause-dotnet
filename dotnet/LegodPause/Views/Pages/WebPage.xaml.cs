using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;

namespace LegodPause.Views.Pages;

public partial class WebPage : Page
{
    private const string leigodLoginUrl = "https://www.leigod.com/m/mlogin.html?region_code=1&language=zh_CN&platform=2";

    private const string UserAgent = "Mozilla/5.0 (Linux; Android 10; SM-G973F) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/110.0.0.0 Mobile Safari/537.36 EdgA/110.0.1587.63";

    public WebPage()
    {
        // CoreWebView2Environment.CreateAsync
        // var opt = (options ?? new CoreWebView2EnvironmentOptions())._nativeICoreWebView2EnvironmentOptions
        InitializeComponent();
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
    }

    private void WebView_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (e.InitializationException != null)
        {
            Console.WriteLine(e.InitializationException);
            throw e.InitializationException;
        }

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
}