using Avalonia.Controls;
using Avalonia.Threading;
using MilOps.Services;
using MilOps.ViewModels;
using System;
using System.Threading.Tasks;

namespace MilOps.Views;

public partial class AppShell : UserControl
{
    public AppShell()
    {
        InitializeComponent();

        // 로그인 이벤트 연결 - DataContext가 설정된 후 연결
        LoginViewControl.DataContextChanged += (s, e) =>
        {
            if (LoginViewControl.DataContext is LoginViewModel vm)
            {
                vm.LoginSuccessful += OnLoginSuccess;
            }
        };

        // 이미 DataContext가 설정되어 있는 경우
        if (LoginViewControl.DataContext is LoginViewModel loginVm)
        {
            loginVm.LoginSuccessful += OnLoginSuccess;
        }
        else
        {
            // Loaded 이벤트에서도 시도
            LoginViewControl.Loaded += (s, e) =>
            {
                if (LoginViewControl.DataContext is LoginViewModel vm)
                {
                    vm.LoginSuccessful += OnLoginSuccess;
                }
            };
        }

        // MainView 로그아웃 이벤트 연결
        MainViewControl.LogoutRequested += OnLogoutRequested;

        // 세션 복원 및 자동 로그인 시도 (비동기)
        _ = TryRestoreSessionAndAutoLoginAsync();
    }

    private async Task TryRestoreSessionAndAutoLoginAsync()
    {
        try
        {
            var restored = await AuthService.TryRestoreSessionAsync();

            if (restored)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    OnLoginSuccess();
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] Session restore failed: {ex.Message}");
        }
    }

    private void ShowLoginView()
    {
        MainViewControl.IsVisible = false;
        LoginViewControl.IsVisible = true;

        if (LoginViewControl.DataContext is LoginViewModel loginVm)
        {
            loginVm.UserId = "";
            loginVm.Password = "";
            loginVm.ErrorMessage = "";
            loginVm.HasError = false;
        }
    }

    private async void OnLoginSuccess()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await FcmService.SaveTokenToServerAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppShell] FCM token save error: {ex.Message}");
            }
        });

        CalendarDataService.PreloadCurrentMonth();
        ScheduleDataService.PreloadCache();

        LoginViewControl.IsVisible = false;
        MainViewControl.IsVisible = true;

        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        await MainViewControl.RefreshUserRoleAsync();
    }

    private void OnLogoutRequested()
    {
        MainViewControl.IsVisible = false;
        LoginViewControl.IsVisible = true;

        if (LoginViewControl.DataContext is LoginViewModel loginVm)
        {
            loginVm.UserId = "";
            loginVm.Password = "";
            loginVm.ErrorMessage = "";
            loginVm.HasError = false;
        }
    }

}
