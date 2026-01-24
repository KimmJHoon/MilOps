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

        // 창이 로드된 후 세션 복원 시도
        Loaded += OnWindowLoaded;
    }

    private async void OnWindowLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[MainWindow] OnWindowLoaded - Starting session restore");

        try
        {
            // Supabase 초기화
            if (!SupabaseService.IsInitialized)
            {
                await SupabaseService.InitializeAsync();
            }

            // 세션 복원 시도
            var restored = await AuthService.TryRestoreSessionAsync();
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Session restored: {restored}");

            // 로딩 화면 숨기고 적절한 화면 표시
            LoadingView.IsVisible = false;

            if (restored)
            {
                // 세션이 복원되면 메인 화면 표시
                MainViewControl.RefreshUserRole();
                MainViewControl.IsVisible = true;
            }
            else
            {
                // 세션이 없으면 로그인 화면 표시
                LoginViewControl.IsVisible = true;
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Session restore error: {ex.Message}");
            // 에러 발생 시 로그인 화면 표시
            LoadingView.IsVisible = false;
            LoginViewControl.IsVisible = true;
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
        System.Diagnostics.Debug.WriteLine("[MainWindow] OnLogout called");
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
        System.Diagnostics.Debug.WriteLine("[MainWindow] OnLogout completed");
    }
}