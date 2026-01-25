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

        if (LoginViewControl.DataContext is LoginViewModel loginVm)
        {
            loginVm.LoginSuccessful += OnLoginSuccess;
            loginVm.SignUpRequested += OnSignUpRequested;
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
                    vm.SignUpRequested += OnSignUpRequested;
                    System.Diagnostics.Debug.WriteLine("[AppShell] LoginSuccessful event connected (on Loaded)");
                }
            };
        }

        // 초대코드 입력 화면 설정
        SetupInviteCodeView();

        // 초대 수락 화면 이벤트 연결
        SetupInviteAcceptView();

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
        // InviteCodeView에 ViewModel 설정
        var inviteCodeVm = new InviteCodeViewModel();
        InviteCodeViewControl.DataContext = inviteCodeVm;

        // 뒤로가기 이벤트
        inviteCodeVm.BackRequested += () =>
        {
            System.Diagnostics.Debug.WriteLine("[AppShell] Back to login requested from InviteCodeView");
            ShowLoginView();
        };

        // 코드 검증 성공 이벤트
        inviteCodeVm.CodeValidated += (invitation) =>
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] Invite code validated, proceeding to registration");
            ShowInviteAcceptViewWithInvitation(invitation);
        };
    }

    private void SetupInviteAcceptView()
    {
        // InviteAcceptView에 ViewModel 설정
        var inviteVm = new InviteAcceptViewModel();
        InviteAcceptViewControl.DataContext = inviteVm;

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
        }
    }

    private void ShowInviteAcceptViewWithInvitation(Invitation invitation)
    {
        // 모든 뷰 숨기기
        LoginViewControl.IsVisible = false;
        MainViewControl.IsVisible = false;
        InviteCodeViewControl.IsVisible = false;
        InviteAcceptViewControl.IsVisible = true;

        // 초대 정보로 ViewModel 초기화
        if (InviteAcceptViewControl.DataContext is InviteAcceptViewModel inviteVm)
        {
            inviteVm.InitializeWithInvitation(invitation);
        }
    }

    private async void ShowInviteAcceptView(string inviteCode)
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

    private void OnLoginSuccess()
    {
        System.Diagnostics.Debug.WriteLine("[AppShell] OnLoginSuccess called");

        // MainView의 역할 정보 갱신
        MainViewControl.RefreshUserRole();

        LoginViewControl.IsVisible = false;
        InviteCodeViewControl.IsVisible = false;
        InviteAcceptViewControl.IsVisible = false;
        MainViewControl.IsVisible = true;

        System.Diagnostics.Debug.WriteLine("[AppShell] OnLoginSuccess completed");
    }

}
