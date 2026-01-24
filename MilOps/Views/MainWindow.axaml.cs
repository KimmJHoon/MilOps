using Avalonia.Controls;
using MilOps.ViewModels;

namespace MilOps.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        if (LoginViewControl.DataContext is LoginViewModel loginVm)
        {
            loginVm.LoginSuccessful += OnLoginSuccess;
        }
    }

    private void OnLoginSuccess()
    {
        LoginViewControl.IsVisible = false;
        MainViewControl.IsVisible = true;
    }
}