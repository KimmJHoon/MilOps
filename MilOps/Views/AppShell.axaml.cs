using Avalonia.Controls;
using MilOps.Services;
using MilOps.ViewModels;

namespace MilOps.Views;

public partial class AppShell : UserControl
{
    public AppShell()
    {
        InitializeComponent();

        // 로그인 이벤트 연결
        if (LoginViewControl.DataContext is LoginViewModel loginVm)
        {
            loginVm.LoginSuccessful += OnLoginSuccess;
        }

        // 로그아웃 이벤트 연결
        MainViewControl.LogoutRequested += OnLogout;

        // 기존 로그인 세션 확인
        if (AuthService.TryRestoreSession())
        {
            OnLoginSuccess();
        }
    }

    private void OnLoginSuccess()
    {
        LoginViewControl.IsVisible = false;
        MainViewControl.IsVisible = true;
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
