using Avalonia.Controls;
using MilOps.Services;
using MilOps.ViewModels;

namespace MilOps.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        System.Diagnostics.Debug.WriteLine($"[MainWindow] LoginViewControl.DataContext: {LoginViewControl.DataContext?.GetType().Name ?? "null"}");

        if (LoginViewControl.DataContext is LoginViewModel loginVm)
        {
            loginVm.LoginSuccessful += OnLoginSuccess;
            System.Diagnostics.Debug.WriteLine("[MainWindow] LoginSuccessful event connected");
        }
        else
        {
            // DataContext가 아직 설정되지 않은 경우, Loaded 이벤트에서 연결
            LoginViewControl.Loaded += (s, e) =>
            {
                if (LoginViewControl.DataContext is LoginViewModel vm)
                {
                    vm.LoginSuccessful += OnLoginSuccess;
                    System.Diagnostics.Debug.WriteLine("[MainWindow] LoginSuccessful event connected (on Loaded)");
                }
            };
        }

        // 로그아웃 이벤트 연결
        MainViewControl.LogoutRequested += OnLogout;

        // 이미 로그인된 상태인지 확인
        if (AuthService.IsLoggedIn)
        {
            OnLoginSuccess();
        }
    }

    private void OnLoginSuccess()
    {
        System.Diagnostics.Debug.WriteLine("[MainWindow] OnLoginSuccess called");

        // MainView의 역할 정보 갱신
        MainViewControl.RefreshUserRole();

        LoginViewControl.IsVisible = false;
        MainViewControl.IsVisible = true;

        System.Diagnostics.Debug.WriteLine("[MainWindow] OnLoginSuccess completed");
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