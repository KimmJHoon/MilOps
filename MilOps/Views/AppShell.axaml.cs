using Avalonia.Controls;
using MilOps.Services;
using MilOps.ViewModels;

namespace MilOps.Views;

public partial class AppShell : UserControl
{
    public AppShell()
    {
        InitializeComponent();

        // 로그인 이벤트 연결 - DataContext가 설정된 후 연결
        System.Diagnostics.Debug.WriteLine($"[AppShell] LoginViewControl.DataContext: {LoginViewControl.DataContext?.GetType().Name ?? "null"}");

        if (LoginViewControl.DataContext is LoginViewModel loginVm)
        {
            loginVm.LoginSuccessful += OnLoginSuccess;
            System.Diagnostics.Debug.WriteLine("[AppShell] LoginSuccessful event connected");
        }
        else
        {
            // DataContext가 아직 설정되지 않은 경우, Loaded 이벤트에서 연결
            LoginViewControl.Loaded += (s, e) =>
            {
                if (LoginViewControl.DataContext is LoginViewModel vm)
                {
                    vm.LoginSuccessful += OnLoginSuccess;
                    System.Diagnostics.Debug.WriteLine("[AppShell] LoginSuccessful event connected (on Loaded)");
                }
            };
        }

        // 로그아웃 이벤트 연결
        MainViewControl.LogoutRequested += OnLogout;

        // 이미 로그인된 상태인지 확인 (App.axaml.cs에서 세션 복원됨)
        if (AuthService.IsLoggedIn)
        {
            OnLoginSuccess();
        }
    }

    private void OnLoginSuccess()
    {
        System.Diagnostics.Debug.WriteLine("[AppShell] OnLoginSuccess called");

        // MainView의 역할 정보 갱신
        MainViewControl.RefreshUserRole();

        LoginViewControl.IsVisible = false;
        MainViewControl.IsVisible = true;

        System.Diagnostics.Debug.WriteLine("[AppShell] OnLoginSuccess completed");
    }

    private void OnLogout()
    {
        MainViewControl.IsVisible = false;
        LoginViewControl.IsVisible = true;

        // 로그인 폼 초기화
        if (LoginViewControl.DataContext is LoginViewModel loginVm)
        {
            loginVm.UserId = "";
            loginVm.Password = "";
            loginVm.ErrorMessage = "";
            loginVm.HasError = false;
        }
    }
}
