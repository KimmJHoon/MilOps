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
                // 세션 복원 성공 → Preload 즉시 시작 (UI 전환 전)
                // LoginViewModel.PreloadDataAfterLogin()과 동일한 프리페치를 자동 로그인 경로에도 적용
                System.Diagnostics.Debug.WriteLine("[PERF][AppShell] 세션 복원 성공 → Preload 시작");
                QueryHelper.PreloadOrgData();  // 파일 캐시 즉시 로드 + 서버 갱신 (stale-while-revalidate)
                QueryHelper.PreloadBizData();  // Company + User + MatchedCompanyIds 공유 캐시
                ScheduleDataService.LoadSchedulesInBackground(AuthService.CurrentUser!);
                CalendarDataService.PreloadCurrentMonth();

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
        var totalSw = System.Diagnostics.Stopwatch.StartNew();
        System.Diagnostics.Debug.WriteLine("[PERF][AppShell] 로그인 성공 → UI 전환 시작");

        // FCM 토큰 저장은 별도 스레드에서 실행
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

        // 데이터 Preload는 LoginViewModel.PreloadDataAfterLogin()에서 이미 수행

        LoginViewControl.IsVisible = false;
        MainViewControl.IsVisible = true;
        System.Diagnostics.Debug.WriteLine($"[PERF][AppShell] MainView.IsVisible=true: {totalSw.ElapsedMilliseconds}ms");

        // Layout 대기 제거: DispatcherPriority.Render/Loaded 대기 모두 UI 스레드가 컨트롤 초기화로
        // 바쁠 때는 전체 초기화 완료까지 블로킹됨 (4,700ms+). 대신 비동기로 즉시 진행.
        // RefreshUserRoleAsync()는 UI visible 설정 후 바로 실행해도 안전함.
        // (ObservableProperty 바인딩은 다음 프레임에서 자동 반영)

        // RefreshUserRole → 데이터 로드는 별도 스레드에서 처리됨
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await MainViewControl.RefreshUserRoleAsync();
        System.Diagnostics.Debug.WriteLine($"[PERF][AppShell] RefreshUserRoleAsync: {sw.ElapsedMilliseconds}ms");
        System.Diagnostics.Debug.WriteLine($"[PERF][AppShell] ===== 로그인→메인 전환 총 소요: {totalSw.ElapsedMilliseconds}ms =====");

        // 대기 중인 딥링크 처리 (알림 탭으로 앱이 cold start된 경우)
        MainViewControl.ProcessPendingDeepLink();
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
