using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;

namespace LegodPause.Views.Pages;

public partial class WebPage : Page
{
    public string Url { get; } = "https://www.leigod.com/m/mlogin.html?region_code=1&language=zh_CN&platform=2";
    private string UserAgent { get; } = "Mozilla/5.0 (Linux; Android 10; SM-G973F) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/110.0.0.0 Mobile Safari/537.36 EdgA/110.0.1587.63";

    public WebPage()
    {
        InitializeComponent();
    }

    private void backBtn_Click(object sender, RoutedEventArgs e)
    {
    }

    private void UIElement_OnTextInput(object sender, TextCompositionEventArgs e)
    {
        Console.WriteLine(e.Text);
    }

    private void WebView_OnNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
    {
    }
}