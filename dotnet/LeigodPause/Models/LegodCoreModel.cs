using CommunityToolkit.Mvvm.ComponentModel;

namespace LeigodPause.Models;

public partial class LeigodCoreModel : ObservableObject
{
    [ObservableProperty]
    private string? _token;
}