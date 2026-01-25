using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace MilOps.Services;

/// <summary>
/// 앱 재시작 서비스 - 플랫폼별 구현 제공
/// </summary>
public static class AppRestartService
{
    /// <summary>
    /// 플랫폼별 앱 재시작 액션
    /// Android/Desktop에서 각각 설정
    /// </summary>
    public static Action? RestartApp { get; set; }

    /// <summary>
    /// 앱 종료 액션 (재시작 불가능한 경우 사용)
    /// </summary>
    public static Action? ExitApp { get; set; }

    /// <summary>
    /// 로그아웃 전 정리 작업 (타이머, Realtime 구독 해제 등)
    /// </summary>
    public static Action? CleanupBeforeLogout { get; set; }

    /// <summary>
    /// 로그아웃 후 앱 재시작 수행
    /// </summary>
    public static async Task LogoutAndRestartAsync()
    {
        try
        {
            Debug.WriteLine("[AppRestartService] Starting logout and restart process...");

            // 0. 정리 작업 먼저 수행 (타이머, Realtime 구독 해제)
            try
            {
                CleanupBeforeLogout?.Invoke();
                Debug.WriteLine("[AppRestartService] Cleanup completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppRestartService] Cleanup error (continuing): {ex.Message}");
            }

            // 1. 세션 저장소 클리어
            SessionStorageService.ClearSession();
            Debug.WriteLine("[AppRestartService] Session cleared");

            // 2. AuthService 로그아웃
            await AuthService.LogoutAsync();
            Debug.WriteLine("[AppRestartService] AuthService logged out");

            // 3. 플랫폼별 앱 재시작
            if (RestartApp != null)
            {
                Debug.WriteLine("[AppRestartService] Executing platform-specific restart...");
                RestartApp.Invoke();
            }
            else if (ExitApp != null)
            {
                Debug.WriteLine("[AppRestartService] Restart not available, exiting app...");
                ExitApp.Invoke();
            }
            else
            {
                Debug.WriteLine("[AppRestartService] No restart/exit handler registered!");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppRestartService] Error during logout/restart: {ex.Message}");
            // 오류 발생 시에도 앱 종료 시도
            ExitApp?.Invoke();
        }
    }
}
