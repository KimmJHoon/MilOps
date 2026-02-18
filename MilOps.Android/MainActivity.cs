using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using Android.Views;
using AndroidX.Activity;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Avalonia;
using Avalonia.Android;
using Firebase;
using Firebase.Messaging;
using MilOps.Config;
using MilOps.Services;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MilOps.Android;

[Activity(
    Label = "@string/app_name",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    LaunchMode = LaunchMode.SingleTop,
    WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    private const string TAG = "MilOps";
    private static MainActivity? _instance;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        try
        {
            _instance = this;

            // Supabase config: Splash에서 이미 로드된 경우 스킵 (중복 파일 I/O 제거)
            if (string.IsNullOrEmpty(SupabaseConfig.Url))
            {
                LoadSupabaseConfig();
            }

            // 앱 재시작 서비스 설정 (가벼운 람다 등록만, 메인 스레드 블로킹 없음)
            SetupAppRestartService();
            SetupPdfViewerService();

            // ★ base.OnCreate() 최우선 — Avalonia Surface 생성을 최대한 빨리 시작
            base.OnCreate(savedInstanceState);

            // 시스템 뒤로가기 버튼 핸들러 등록 (API 33+ 대응)
            OnBackPressedDispatcher.AddCallback(this, new BackPressedCallback(this));

            // ★ Firebase 초기화를 base.OnCreate() 이후로 지연
            // FirebaseApp.InitializeApp()은 메인 스레드 필수이나, Avalonia Surface 생성 이후에
            // 호출하면 첫 프레임 렌더링과 겹치지 않아 Choreographer 프레임 드랍 감소
            InitializeFirebaseDeferred();

            // 알림 탭으로 앱이 열린 경우 딥링크 처리 (cold start)
            HandleNotificationIntent(Intent);
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"OnCreate Error: {ex.Message}");
            Log.Error(TAG, $"StackTrace: {ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// 앱이 이미 실행 중일 때 알림 탭으로 인텐트 수신 (LaunchMode.SingleTop)
    /// </summary>
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        if (intent != null)
        {
            HandleNotificationIntent(intent);
        }
    }

    /// <summary>
    /// 알림 인텐트에서 딥링크 정보 추출 → DeepLinkService로 전달
    /// </summary>
    private void HandleNotificationIntent(Intent? intent)
    {
        if (intent == null) return;

        var targetTab = intent.GetStringExtra("target_tab");
        var notificationType = intent.GetStringExtra("notification_type");

        if (!string.IsNullOrEmpty(targetTab))
        {
            Log.Info(TAG, $"Notification intent: tab={targetTab}, type={notificationType}");
            DeepLinkService.RequestNavigation(targetTab, notificationType);

            // 처리 후 extras 제거 (중복 처리 방지)
            intent.RemoveExtra("target_tab");
            intent.RemoveExtra("notification_type");
        }
    }

    /// <summary>
    /// Firebase 초기화를 다음 메인 루퍼 사이클로 지연 (첫 프레임 렌더링 후 실행)
    /// → Choreographer 프레임 드랍 감소
    /// </summary>
    private void InitializeFirebaseDeferred()
    {
        // 다음 메인 루퍼 사이클에 Firebase 초기화 예약 → 첫 프레임 렌더링 방해 안 함
        new Handler(Looper.MainLooper!).Post(() =>
        {
            InitializeFirebase();
            // 알림 권한도 Firebase 이후에 요청 (UI 표시 완료 후)
            RequestNotificationPermission();
        });
    }

    /// <summary>
    /// 앱 재시작 서비스 설정
    /// </summary>
    private void SetupAppRestartService()
    {
        AppRestartService.RestartApp = () =>
        {
            Log.Info(TAG, "Restarting app...");

            // 앱 재시작 인텐트 생성
            var intent = PackageManager?.GetLaunchIntentForPackage(PackageName ?? "");
            if (intent != null)
            {
                intent.AddFlags(ActivityFlags.ClearTop | ActivityFlags.NewTask | ActivityFlags.ClearTask);

                // PendingIntent로 앱 재시작 예약
                var flags = PendingIntentFlags.CancelCurrent;
                if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
                {
#pragma warning disable CA1416
                    flags |= PendingIntentFlags.Immutable;
#pragma warning restore CA1416
                }

                var pendingIntent = PendingIntent.GetActivity(
                    this,
                    0,
                    intent,
                    flags);

                // AlarmManager로 100ms 후 재시작
                var alarmManager = GetSystemService(AlarmService) as AlarmManager;
                if (pendingIntent != null)
                {
                    alarmManager?.Set(
                        AlarmType.Rtc,
                        Java.Lang.JavaSystem.CurrentTimeMillis() + 100,
                        pendingIntent);
                }

                // 현재 액티비티 종료
                FinishAffinity();
                Java.Lang.JavaSystem.Exit(0);
            }
        };

        AppRestartService.ExitApp = () =>
        {
            Log.Info(TAG, "Exiting app...");
            FinishAffinity();
            Java.Lang.JavaSystem.Exit(0);
        };
    }

    /// <summary>
    /// Firebase 초기화
    /// </summary>
    private void InitializeFirebase()
    {
        try
        {
            FirebaseApp.InitializeApp(this);
            Log.Info(TAG, "Firebase initialized successfully");

            // FcmService 설정
            SetupFcmService();

            // FCM 토큰 가져오기 (비동기)
            Task.Run(async () =>
            {
                var token = await FcmTokenHolder.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    Log.Info(TAG, $"FCM Token: {token.Substring(0, Math.Min(20, token.Length))}...");
                    FcmService.CurrentToken = token;
                }
            });
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"Firebase initialization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// FcmService 설정
    /// </summary>
    private void SetupFcmService()
    {
        // FCM 토큰 제공자 설정
        FcmService.TokenProvider = async () =>
        {
            return await FcmTokenHolder.GetTokenAsync();
        };

        // 디바이스 이름 제공자 설정
        FcmService.DeviceNameProvider = () =>
        {
            return Build.Model ?? "Android Device";
        };
    }

    /// <summary>
    /// PDF 뷰어 서비스 설정 — EmbeddedResource PDF를 Android 기본 뷰어로 열기
    /// </summary>
    private void SetupPdfViewerService()
    {
        PdfViewerService.OpenPdfFromAssets = (resourceName) =>
        {
            try
            {
                // 1. EmbeddedResource에서 읽기
                var assembly = typeof(PdfViewerService).Assembly;
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    Log.Warn(TAG, $"PDF resource not found: {resourceName}");
                    return;
                }

                // 2. 캐시 디렉토리에 복사
                var cacheDir = CacheDir;
                if (cacheDir == null) return;

                var file = new Java.IO.File(cacheDir, resourceName);
                using (var output = System.IO.File.Create(file.AbsolutePath))
                {
                    stream.CopyTo(output);
                }

                // 3. FileProvider URI 생성
                var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(
                    this, $"{PackageName}.fileprovider", file);

                // 4. Intent로 PDF 뷰어 열기
                var intent = new Intent(Intent.ActionView);
                intent.SetDataAndType(uri, "application/pdf");
                intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.NewTask);
                StartActivity(intent);
            }
            catch (Exception ex)
            {
                Log.Error(TAG, $"PDF open error: {ex.Message}");
            }
        };
    }

    /// <summary>
    /// Android 13+ 알림 권한 요청
    /// </summary>
    private void RequestNotificationPermission()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
        {
#pragma warning disable CA1416 // 런타임 버전 체크 완료
            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.PostNotifications)
                != Permission.Granted)
            {
                ActivityCompat.RequestPermissions(
                    this,
                    new[] { Manifest.Permission.PostNotifications },
                    1001);
            }
#pragma warning restore CA1416
        }
    }

    private void LoadSupabaseConfig()
    {
        try
        {
            using var stream = Assets?.Open("config.env");
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                var content = reader.ReadToEnd();
                ParseEnvContent(content);
                Log.Info("MilOps", "Supabase config loaded from Assets/config.env");
            }
        }
        catch (Java.IO.FileNotFoundException)
        {
            Log.Warn("MilOps", "config.env not found in Assets");
        }
    }

    private void ParseEnvContent(string content)
    {
        var lines = content.Split('\n', '\r');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;

            var parts = trimmed.Split('=', 2);
            if (parts.Length != 2)
                continue;

            var key = parts[0].Trim();
            var value = parts[1].Trim().Trim('"');

            if (key == "SUPABASE_URL")
                SupabaseConfig.Url = value;
            else if (key == "SUPABASE_ANON_KEY")
                SupabaseConfig.AnonKey = value;
            else if (key == "CHAT_SERVER_HOST")
                ChatServerConfig.Host = value;
            else if (key == "CHAT_SERVER_PORT" && int.TryParse(value, out var port))
                ChatServerConfig.Port = port;
        }
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .LogToTrace();
    }

    /// <summary>
    /// 시스템 뒤로가기 버튼 처리 콜백 (OnBackPressedDispatcher 방식, API 33+ 대응)
    /// </summary>
    private class BackPressedCallback : OnBackPressedCallback
    {
        private readonly MainActivity _activity;

        public BackPressedCallback(MainActivity activity) : base(true)
        {
            _activity = activity;
        }

        public override void HandleOnBackPressed()
        {
            // Avalonia 앱에서 뒤로가기 처리 (오버레이 닫기 등)
            if (AppRestartService.OnBackPressed?.Invoke() == true)
            {
                return;
            }

            // 처리되지 않으면 앱을 백그라운드로 보냄 (종료하지 않음)
            _activity.MoveTaskToBack(true);
        }
    }
}
