using CommunityToolkit.Mvvm.ComponentModel;

namespace MilOps.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotLoggedIn))]
    private bool _isLoggedIn = false;

    public bool IsNotLoggedIn => !IsLoggedIn;

    public void OnLoginSuccess()
    {
        IsLoggedIn = true;
    }
}
