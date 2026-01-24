using Avalonia.Controls;
using MilOps.ViewModels;

namespace MilOps.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
        DataContext = new LoginViewModel();
    }
}
