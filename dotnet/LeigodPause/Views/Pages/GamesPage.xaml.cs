namespace LeigodPause.Views.Pages;

public partial class GamesPage : Page
{
    public LeigodCoreModel LeigodCoreModel { get; } = App.Host.Services.GetRequiredService<LeigodCoreModel>();

    public GamesPage()
    {
        InitializeComponent();
    }

    private void AddOnClick(object sender, RoutedEventArgs e)
    {
        var fileDialog = new OpenFileDialog()
        {
            Multiselect = false,
            Filter = "可执行文件 (*.exe)|*.exe"
        };
        if (true == fileDialog.ShowDialog())
        {
            var fileName = fileDialog.FileName;
            var appName = Path.GetFileNameWithoutExtension(fileName);
            if (!LeigodCoreModel.GameListSource.Contains(appName))
            {
                LeigodCoreModel.GameListSource.Add(appName);
            }
        }
    }

    private void ListItemOnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: string appName })
        {
            if (LeigodCoreModel.GameListSource.Contains(appName))
            {
                LeigodCoreModel.GameListSource.Remove(appName);
            }
        }
    }
}