using Avalonia.Controls;
using Avalonia.Threading;
using MilOps.Models;
using MilOps.Services;
using MilOps.ViewModels;
using System;
using System.Threading.Tasks;

namespace MilOps.Views;

public partial class AppShell : UserControl
{
    private DispatcherTimer? _deepLinkCheckTimer;

    public AppShell()
    {
        InitializeComponent();

        // 로그인 이벤트 연결 - DataContext가 설정된 후 연결
        System.Diagnostics.Debug.WriteLine($"[AppShell] LoginViewControl.DataContext: {LoginViewControl.DataContext?.GetType().Name ?? "null"}");

        // DataContextChanged 이벤트로 확실하게 연결
        LoginViewControl.DataContextChanged += (s, e) =>
        {
            if (LoginViewControl.DataContext is LoginViewModel vm)
            {
                vm.LoginSuccessful += OnLoginSuccess;
                vm.SignUpRequested += OnSignUpRequested;
                System.Diagnostics.Debug.WriteLine("[AppShell] LoginView events connected (on DataContextChanged)");
            }
        };

        // 이미 DataContext가 설정되어 있는 경우
        if (LoginViewControl.DataContext is LoginViewModel loginVm)
        {
            loginVm.LoginSuccessful += OnLoginSuccess;
            loginVm.SignUpRequested += OnSignUpRequested;
            System.Diagnostics.Debug.WriteLine("[AppShell] LoginView events connected (immediate)");
        }
        else
        {
            // Loaded 이벤트에서도 시도
            LoginViewControl.Loaded += (s, e) =>
            {
                if (LoginViewControl.DataContext is LoginViewModel vm)
                {
                    vm.LoginSuccessful += OnLoginSuccess;
                    vm.SignUpRequested += OnSignUpRequested;
                    System.Diagnostics.Debug.WriteLine("[AppShell] LoginView events connected (on Loaded)");
                }
            };
        }

        // 초대코드 입력 화면 설정
        SetupInviteCodeView();

        // 초대 수락 화면 이벤트 연결
        SetupInviteAcceptView();

        // MainView 로그아웃 이벤트 연결
        MainViewControl.LogoutRequested += OnLogoutRequested;

        // 딥링크 확인 (앱 시작 시)
        CheckForDeepLink();

        // 딥링크 주기적 확인 (앱 실행 중 딥링크 수신 대비)
        StartDeepLinkCheckTimer();

        // 세션 복원 및 자동 로그인 시도 (비동기)
        _ = TryRestoreSessionAndAutoLoginAsync();
    }

    /// <summary>
    /// 세션 복원 및 자동 로그인 시도
    /// </summary>
    private async Task TryRestoreSessionAndAutoLoginAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[AppShell] Attempting to restore session...");

            var restored = await AuthService.TryRestoreSessionAsync();

            if (restored && !IsInviteAcceptVisible())
            {
                System.Diagnostics.Debug.WriteLine("[AppShell] Session restored, auto-login successful");
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    OnLoginSuccess();
                });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[AppShell] No session to restore or invite view is visible");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] Session restore failed: {ex.Message}");
        }
    }

    private void SetupInviteCodeView()
    {
        System.Diagnostics.Debug.WriteLine("[AppShell] SetupInviteCodeView called");
        try
        {
            // InviteCodeView에 ViewModel 설정
            var inviteCodeVm = new InviteCodeViewModel();
            InviteCodeViewControl.DataContext = inviteCodeVm;
            System.Diagnostics.Debug.WriteLine("[AppShell] InviteCodeViewModel created and set");

            // 뒤로가기 이벤트
            inviteCodeVm.BackRequested += () =>
            {
                System.Diagnostics.Debug.WriteLine("[AppShell] Back to login requested from InviteCodeView");
                ShowLoginView();
            };

            // 코드 검증 성공 이벤트
            inviteCodeVm.CodeValidated += (invitation) =>
            {
                System.Diagnostics.Debug.WriteLine($"[AppShell] Invite code validated: {invitation?.InviteCode}, proceeding to registration");
                if (invitation != null)
                {
                    ShowInviteAcceptViewWithInvitation(invitation);
                }
            };
            System.Diagnostics.Debug.WriteLine("[AppShell] SetupInviteCodeView completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] SetupInviteCodeView error: {ex.GetType().Name}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[AppShell] StackTrace: {ex.StackTrace}");
        }
    }

    private void SetupInviteAcceptView()
    {
        System.Diagnostics.Debug.WriteLine("[AppShell] SetupInviteAcceptView called");
        try
        {
            // InviteAcceptView에 ViewModel 설정
            var inviteVm = new InviteAcceptViewModel();
            InviteAcceptViewControl.DataContext = inviteVm;
            System.Diagnostics.Debug.WriteLine("[AppShell] InviteAcceptViewModel created and set");

            // 회원가입 성공 이벤트
            inviteVm.RegistrationSuccessful += () =>
            {
                System.Diagnostics.Debug.WriteLine("[AppShell] Registration successful, navigating to login");
                ShowLoginView();
            };

            // 로그인 화면으로 이동 이벤트
            inviteVm.NavigateToLogin += () =>
            {
                System.Diagnostics.Debug.WriteLine("[AppShell] Navigate to login requested");
                ShowLoginView();
            };
            System.Diagnostics.Debug.WriteLine("[AppShell] SetupInviteAcceptView completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] SetupInviteAcceptView error: {ex.GetType().Name}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[AppShell] StackTrace: {ex.StackTrace}");
        }
    }

    private void StartDeepLinkCheckTimer()
    {
        _deepLinkCheckTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _deepLinkCheckTimer.Tick += (s, e) => CheckForDeepLink();
        _deepLinkCheckTimer.Start();
    }

    private void CheckForDeepLink()
    {
        // DeepLinkHandler는 Android 프로젝트에 있으므로 리플렉션 또는 정적 접근 사용
        var inviteCode = GetPendingInviteCode();
        if (!string.IsNullOrEmpty(inviteCode))
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] Deep link detected: {inviteCode}");
            ShowInviteAcceptView(inviteCode);
        }
    }

    private string? GetPendingInviteCode()
    {
        try
        {
            // Android의 DeepLinkHandler.ConsumePendingInviteCode() 호출
            var deepLinkType = Type.GetType("MilOps.Android.DeepLinkHandler, MilOps.Android");
            if (deepLinkType != null)
            {
                var method = deepLinkType.GetMethod("ConsumePendingInviteCode");
                if (method != null)
                {
                    return method.Invoke(null, null) as string;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] GetPendingInviteCode error: {ex.Message}");
        }
        return null;
    }

    private bool IsInviteAcceptVisible()
    {
        return InviteAcceptViewControl.IsVisible;
    }

    private void OnSignUpRequested()
    {
        System.Diagnostics.Debug.WriteLine("[AppShell] SignUp requested, showing InviteCodeView");
        ShowInviteCodeView();
    }

    private void ShowInviteCodeView()
    {
        System.Diagnostics.Debug.WriteLine("[AppShell] ShowInviteCodeView called");
        try
        {
            // 모든 뷰 숨기기
            LoginViewControl.IsVisible = false;
            MainViewControl.IsVisible = false;
            InviteAcceptViewControl.IsVisible = false;
            InviteCodeViewControl.IsVisible = true;

            // 초대코드 입력 폼 초기화
            if (InviteCodeViewControl.DataContext is InviteCodeViewModel inviteCodeVm)
            {
                inviteCodeVm.InviteCode = "";
                inviteCodeVm.ErrorMessage = "";
                inviteCodeVm.HasError = false;
                System.Diagnostics.Debug.WriteLine("[AppShell] InviteCodeView form reset");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[AppShell] WARNING: InviteCodeViewControl.DataContext is not InviteCodeViewModel");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] ShowInviteCodeView error: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void ShowInviteAcceptViewWithInvitation(Invitation invitation)
    {
        System.Diagnostics.Debug.WriteLine($"[AppShell] ShowInviteAcceptViewWithInvitation called, invitation: {invitation?.InviteCode}");
        try
        {
            // 모든 뷰 숨기기
            LoginViewControl.IsVisible = false;
            MainViewControl.IsVisible = false;
            InviteCodeViewControl.IsVisible = false;
            InviteAcceptViewControl.IsVisible = true;

            // 초대 정보로 ViewModel 초기화
            if (InviteAcceptViewControl.DataContext is InviteAcceptViewModel inviteVm && invitation != null)
            {
                System.Diagnostics.Debug.WriteLine("[AppShell] Initializing InviteAcceptViewModel with invitation");
                inviteVm.InitializeWithInvitation(invitation);
                System.Diagnostics.Debug.WriteLine("[AppShell] InviteAcceptViewModel initialized");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[AppShell] WARNING: InviteAcceptViewControl.DataContext is not InviteAcceptViewModel");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] ShowInviteAcceptViewWithInvitation error: {ex.GetType().Name}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[AppShell] StackTrace: {ex.StackTrace}");
        }
    }

    private async void ShowInviteAcceptView(string inviteCode)
    {
        try
        {
            // 모든 뷰 숨기기
            LoginViewControl.IsVisible = false;
            MainViewControl.IsVisible = false;
            InviteCodeViewControl.IsVisible = false;
            InviteAcceptViewControl.IsVisible = true;

            // 초대 코드로 ViewModel 초기화
            if (InviteAcceptViewControl.DataContext is InviteAcceptViewModel inviteVm)
            {
                await inviteVm.InitializeWithInviteCodeAsync(inviteCode);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] ShowInviteAcceptView error: {ex.Message}");
            // 에러 발생 시 로그인 화면으로 복귀
            ShowLoginView();
        }
    }

    private void ShowLoginView()
    {
        InviteAcceptViewControl.IsVisible = false;
        InviteCodeViewControl.IsVisible = false;
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

    private async void OnLoginSuccess()
    {
        System.Diagnostics.Debug.WriteLine("[AppShell] OnLoginSuccess called");

        // FCM 토큰 서버에 저장 (Android에서만 작동)
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

        // ★ 화면 전환 전에 데이터 미리 로드 시작 (Preload)
        // 이렇게 하면 화면 전환 후 즉시 로딩 스피너가 표시되고, 빈 화면 시간이 최소화됨
        CalendarDataService.PreloadCurrentMonth();
        ScheduleDataService.PreloadCache();  // 일정 목록용 캐시 미리 로드
        System.Diagnostics.Debug.WriteLine("[AppShell] Calendar and Schedule preload started");

        // 화면 전환
        LoginViewControl.IsVisible = false;
        InviteCodeViewControl.IsVisible = false;
        InviteAcceptViewControl.IsVisible = false;
        MainViewControl.IsVisible = true;

        // 렌더링 완료까지 대기 (DispatcherPriority.Loaded = UI 그리기 완료 후 실행)
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);

        // MainView가 visible된 후 역할 정보 갱신 및 초기화
        await MainViewControl.RefreshUserRoleAsync();

        System.Diagnostics.Debug.WriteLine("[AppShell] OnLoginSuccess completed - data loading finished");
    }

    private void OnLogoutRequested()
    {
        System.Diagnostics.Debug.WriteLine("[AppShell] OnLogoutRequested - navigating to login screen");

        // 모든 화면 숨기고 로그인 화면 표시
        MainViewControl.IsVisible = false;
        InviteAcceptViewControl.IsVisible = false;
        InviteCodeViewControl.IsVisible = false;
        LoginViewControl.IsVisible = true;

        // 로그인 폼 초기화
        if (LoginViewControl.DataContext is LoginViewModel loginVm)
        {
            loginVm.UserId = "";
            loginVm.Password = "";
            loginVm.ErrorMessage = "";
            loginVm.HasError = false;
        }

        System.Diagnostics.Debug.WriteLine("[AppShell] OnLogoutRequested completed");
    }

}
