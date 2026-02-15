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
        LoginViewControl.DataContextChanged += (s, e) =>
        {
            if (LoginViewControl.DataContext is LoginViewModel vm)
            {
                vm.LoginSuccessful += OnLoginSuccess;
                vm.SignUpRequested += OnSignUpRequested;
            }
        };

        // 이미 DataContext가 설정되어 있는 경우
        if (LoginViewControl.DataContext is LoginViewModel loginVm)
        {
            loginVm.LoginSuccessful += OnLoginSuccess;
            loginVm.SignUpRequested += OnSignUpRequested;
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

    private async Task TryRestoreSessionAndAutoLoginAsync()
    {
        try
        {
            var restored = await AuthService.TryRestoreSessionAsync();

            if (restored && !IsInviteAcceptVisible())
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

    private void SetupInviteCodeView()
    {
        try
        {
            var inviteCodeVm = new InviteCodeViewModel();
            InviteCodeViewControl.DataContext = inviteCodeVm;

            inviteCodeVm.BackRequested += () => ShowLoginView();

            inviteCodeVm.CodeValidated += (invitation) =>
            {
                if (invitation != null)
                {
                    ShowInviteAcceptViewWithInvitation(invitation);
                }
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] SetupInviteCodeView error: {ex.Message}");
        }
    }

    private void SetupInviteAcceptView()
    {
        try
        {
            var inviteVm = new InviteAcceptViewModel();
            InviteAcceptViewControl.DataContext = inviteVm;

            inviteVm.RegistrationSuccessful += () => ShowLoginView();
            inviteVm.NavigateToLogin += () => ShowLoginView();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] SetupInviteAcceptView error: {ex.Message}");
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
        var inviteCode = GetPendingInviteCode();
        if (!string.IsNullOrEmpty(inviteCode))
        {
            ShowInviteAcceptView(inviteCode);
        }
    }

    private string? GetPendingInviteCode()
    {
        try
        {
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
        ShowInviteCodeView();
    }

    private void ShowInviteCodeView()
    {
        try
        {
            LoginViewControl.IsVisible = false;
            MainViewControl.IsVisible = false;
            InviteAcceptViewControl.IsVisible = false;
            InviteCodeViewControl.IsVisible = true;

            if (InviteCodeViewControl.DataContext is InviteCodeViewModel inviteCodeVm)
            {
                inviteCodeVm.InviteCode = "";
                inviteCodeVm.ErrorMessage = "";
                inviteCodeVm.HasError = false;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[AppShell] WARNING: InviteCodeViewControl.DataContext is not InviteCodeViewModel");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] ShowInviteCodeView error: {ex.Message}");
        }
    }

    private void ShowInviteAcceptViewWithInvitation(Invitation invitation)
    {
        try
        {
            LoginViewControl.IsVisible = false;
            MainViewControl.IsVisible = false;
            InviteCodeViewControl.IsVisible = false;
            InviteAcceptViewControl.IsVisible = true;

            if (InviteAcceptViewControl.DataContext is InviteAcceptViewModel inviteVm && invitation != null)
            {
                inviteVm.InitializeWithInvitation(invitation);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[AppShell] WARNING: InviteAcceptViewControl.DataContext is not InviteAcceptViewModel");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] ShowInviteAcceptViewWithInvitation error: {ex.Message}");
        }
    }

    private async void ShowInviteAcceptView(string inviteCode)
    {
        try
        {
            LoginViewControl.IsVisible = false;
            MainViewControl.IsVisible = false;
            InviteCodeViewControl.IsVisible = false;
            InviteAcceptViewControl.IsVisible = true;

            if (InviteAcceptViewControl.DataContext is InviteAcceptViewModel inviteVm)
            {
                await inviteVm.InitializeWithInviteCodeAsync(inviteCode);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppShell] ShowInviteAcceptView error: {ex.Message}");
            ShowLoginView();
        }
    }

    private void ShowLoginView()
    {
        InviteAcceptViewControl.IsVisible = false;
        InviteCodeViewControl.IsVisible = false;
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
        InviteCodeViewControl.IsVisible = false;
        InviteAcceptViewControl.IsVisible = false;
        MainViewControl.IsVisible = true;

        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        await MainViewControl.RefreshUserRoleAsync();
    }

    private void OnLogoutRequested()
    {
        MainViewControl.IsVisible = false;
        InviteAcceptViewControl.IsVisible = false;
        InviteCodeViewControl.IsVisible = false;
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
