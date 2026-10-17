using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LeigodPause.Views;

namespace LeigodPause.Models;

public partial class MainViewCommand : ObservableObject
{
    [RelayCommand]
    private void MenuItemCloseView()
    {
        // 命令执行时 MainView 单例必然已构造完成，避免构造期形成循环依赖。
        _ = App.Host.Services.GetRequiredService<MainView>().ExitApplicationAsync();
    }

    [RelayCommand]
    private void MenuItemPauseCloseView()
    {
        var mainView = App.Host.Services.GetRequiredService<MainView>();
        _ = mainView.PauseAndExitAsync();
    }
}