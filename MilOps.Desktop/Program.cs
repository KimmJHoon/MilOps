using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using MilOps.Services;

namespace MilOps.Desktop;

sealed class Program
{
    private static string[]? _args;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        _args = args;
        SetupAppRestartService();
        SetupFcmServiceForDesktop();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    /// <summary>
    /// Desktop용 FcmService 설정
    /// - DeviceType을 "desktop"으로 설정
    /// - DeviceNameProvider를 PC 이름으로 설정
    /// - TokenProvider를 머신 고유 ID 기반으로 설정 (FCM 토큰 대용)
    /// </summary>
    private static void SetupFcmServiceForDesktop()
    {
        FcmService.DeviceType = "desktop";
        FcmService.DeviceNameProvider = () => Environment.MachineName ?? "Desktop";

        // Desktop은 FCM 토큰이 없으므로 머신 고유 식별자를 토큰 대용으로 사용
        // "desktop_{MachineName}_{UserName}" 형식으로 고유 토큰 생성
        var desktopToken = $"desktop_{Environment.MachineName}_{Environment.UserName}";
        FcmService.CurrentToken = desktopToken;
        FcmService.TokenProvider = () => System.Threading.Tasks.Task.FromResult<string?>(desktopToken);

        Debug.WriteLine($"[Program] FcmService configured for Desktop: token={desktopToken}");
    }

    private static void SetupAppRestartService()
    {
        // Desktop 앱 재시작 핸들러 설정
        AppRestartService.RestartApp = () =>
        {
            try
            {
                Debug.WriteLine("[Program] Attempting to restart desktop app...");

                // 현재 실행 파일 경로 가져오기
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath))
                {
                    exePath = Process.GetCurrentProcess().MainModule?.FileName;
                }

                if (!string.IsNullOrEmpty(exePath))
                {
                    Debug.WriteLine($"[Program] Starting new process: {exePath}");

                    // 새 프로세스 시작
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true,
                        Arguments = string.Join(" ", _args ?? Array.Empty<string>())
                    };
                    Process.Start(startInfo);

                    Debug.WriteLine("[Program] New process started, shutting down current...");
                }

                // 현재 앱 종료
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
                else
                {
                    Environment.Exit(0);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Program] Error restarting app: {ex.Message}");
                Environment.Exit(0);
            }
        };

        AppRestartService.ExitApp = () =>
        {
            Debug.WriteLine("[Program] Exiting desktop app...");
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
            else
            {
                Environment.Exit(0);
            }
        };

        Debug.WriteLine("[Program] AppRestartService handlers configured for Desktop");
    }
}
