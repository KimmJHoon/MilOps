using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MilOps.Services;
using System;
using System.Threading.Tasks;

namespace MilOps.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _userId = "";

    [ObservableProperty]
    private string _password = "";

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private bool _hasError = false;

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private bool _isDebugMode = false;

    public event Action? LoginSuccessful;
    public event Action? SignUpRequested;

    private const string QuickLoginPassword = "qwer1234";

    public LoginViewModel()
    {
#if DEBUG
        IsDebugMode = true;
#endif
    }

    [RelayCommand]
    private void OpenSignUp()
    {
        System.Diagnostics.Debug.WriteLine("[LoginViewModel] OpenSignUp called");
        SignUpRequested?.Invoke();
        System.Diagnostics.Debug.WriteLine("[LoginViewModel] SignUpRequested invoked");
    }

    [RelayCommand]
    private async Task QuickLoginAsync(string loginId)
    {
        System.Diagnostics.Debug.WriteLine($"[LoginViewModel] QuickLogin - {loginId}");
        UserId = loginId;
        Password = QuickLoginPassword;
        await LoginAsync();
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        System.Diagnostics.Debug.WriteLine($"[LoginViewModel] LoginAsync started - UserId: {UserId}");

        HasError = false;
        ErrorMessage = "";

        if (string.IsNullOrWhiteSpace(UserId))
        {
            System.Diagnostics.Debug.WriteLine("[LoginViewModel] UserId is empty");
            ErrorMessage = "아이디를 입력하세요";
            HasError = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            System.Diagnostics.Debug.WriteLine("[LoginViewModel] Password is empty");
            ErrorMessage = "비밀번호를 입력하세요";
            HasError = true;
            return;
        }

        IsLoading = true;
        System.Diagnostics.Debug.WriteLine("[LoginViewModel] Starting login process...");

        try
        {
            // Supabase 초기화
            if (!SupabaseService.IsInitialized)
            {
                System.Diagnostics.Debug.WriteLine("[LoginViewModel] Initializing Supabase...");
                await SupabaseService.InitializeAsync();
            }

            System.Diagnostics.Debug.WriteLine("[LoginViewModel] Calling AuthService.LoginAsync...");
            // Supabase Auth로 로그인
            var (success, errorMessage) = await AuthService.LoginAsync(UserId, Password);
            System.Diagnostics.Debug.WriteLine($"[LoginViewModel] Login result - Success: {success}, Error: {errorMessage}");

            if (success)
            {
                System.Diagnostics.Debug.WriteLine("[LoginViewModel] Login successful, starting preload...");

                // 🚀 Preload: 로그인 성공 직후 데이터 미리 로드 시작
                // UI 전환 전에 백그라운드에서 데이터 로딩 시작
                PreloadDataAfterLogin();

                System.Diagnostics.Debug.WriteLine("[LoginViewModel] Invoking LoginSuccessful event");
                LoginSuccessful?.Invoke();
            }
            else
            {
                ErrorMessage = errorMessage ?? "로그인에 실패했습니다";
                HasError = true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoginViewModel] Exception: {ex.Message}");
            ErrorMessage = $"오류가 발생했습니다: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsLoading = false;
            System.Diagnostics.Debug.WriteLine("[LoginViewModel] LoginAsync completed");
        }
    }

    /// <summary>
    /// 로그인 성공 직후 데이터 미리 로드 (Preload)
    /// UI 전환 애니메이션 동안 백그라운드에서 데이터 로딩
    /// </summary>
    private void PreloadDataAfterLogin()
    {
        var currentUser = AuthService.CurrentUser;
        if (currentUser == null) return;

        System.Diagnostics.Debug.WriteLine($"[LoginViewModel] [Preload] Starting data preload for {currentUser.LoginId}");

        // 일정 목록 미리 로드 (fire-and-forget)
        ScheduleDataService.LoadSchedulesInBackground(currentUser);

        // 캘린더 현재 월 미리 로드
        CalendarDataService.PreloadCurrentMonth();

        System.Diagnostics.Debug.WriteLine("[LoginViewModel] [Preload] Data preload started");
    }
}
