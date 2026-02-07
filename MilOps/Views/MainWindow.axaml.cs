using Avalonia.Controls;
using MilOps.Services;
using MilOps.ViewModels;
using System;
using System.Threading.Tasks;

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
        else
        {
            LoginViewControl.Loaded += (s, e) =>
            {
                if (LoginViewControl.DataContext is LoginViewModel vm)
                {
                    vm.LoginSuccessful += OnLoginSuccess;
                }
            };
        }

        Loaded += OnWindowLoaded;
    }

    private async void OnWindowLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            if (!SupabaseService.IsInitialized)
            {
                await SupabaseService.InitializeAsync();
            }

            var restored = await AuthService.TryRestoreSessionAsync();

            LoadingView.IsVisible = false;

            if (restored)
            {
                MainViewControl.RefreshUserRole();
                MainViewControl.IsVisible = true;
            }
            else
            {
                LoginViewControl.IsVisible = true;
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Session restore error: {ex.Message}");
            LoadingView.IsVisible = false;
            LoginViewControl.IsVisible = true;
        }
    }

    private void OnLoginSuccess()
    {
        MainViewControl.RefreshUserRole();

        _ = Task.Run(async () =>
        {
            try
            {
                await FcmService.SaveTokenToServerAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] FCM token save error: {ex.Message}");
            }
        });

        LoginViewControl.IsVisible = false;
        MainViewControl.IsVisible = true;
    }

}
