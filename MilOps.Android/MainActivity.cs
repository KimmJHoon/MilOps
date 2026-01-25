using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Util;
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
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "milops",
    DataHost = "invite")]
public class MainActivity : AvaloniaMainActivity<App>
{
    private const string TAG = "MilOps";
    private static MainActivity? _instance;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        try
        {
            _instance = this;

            // Android Assets에서 .env 파일 읽어서 SupabaseConfig에 설정
            LoadSupabaseConfig();

            // Firebase 초기화
            InitializeFirebase();

            // 앱 재시작 서비스 설정
            SetupAppRestartService();

            base.OnCreate(savedInstanceState);

            // Android 13+ 알림 권한 요청
            RequestNotificationPermission();

            // 딥링크 처리 (앱이 시작될 때)
            HandleIntent(Intent);
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"OnCreate Error: {ex.Message}");
            Log.Error(TAG, $"StackTrace: {ex.StackTrace}");
            throw;
        }
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
                var pendingIntent = PendingIntent.GetActivity(
                    this,
                    0,
                    intent,
                    PendingIntentFlags.CancelCurrent | PendingIntentFlags.Immutable);

                // AlarmManager로 100ms 후 재시작
                var alarmManager = GetSystemService(AlarmService) as AlarmManager;
                alarmManager?.Set(
                    AlarmType.Rtc,
                    Java.Lang.JavaSystem.CurrentTimeMillis() + 100,
                    pendingIntent);

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
    /// 앱이 이미 실행 중일 때 새 인텐트 수신 (딥링크)
    /// </summary>
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleIntent(intent);
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

            // FCM 토큰 가져오기 (비동기)
            Task.Run(async () =>
            {
                var token = await FcmTokenHolder.GetTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    Log.Info(TAG, $"FCM Token: {token.Substring(0, Math.Min(20, token.Length))}...");
                }
            });
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"Firebase initialization failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Android 13+ 알림 권한 요청
    /// </summary>
    private void RequestNotificationPermission()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
        {
            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.PostNotifications)
                != Permission.Granted)
            {
                ActivityCompat.RequestPermissions(
                    this,
                    new[] { Manifest.Permission.PostNotifications },
                    1001);
            }
        }
    }

    /// <summary>
    /// 딥링크 인텐트 처리
    /// </summary>
    private void HandleIntent(Intent? intent)
    {
        if (intent?.Data == null)
            return;

        var uri = intent.Data;
        Log.Info(TAG, $"Deep link received: {uri}");

        // milops://invite/{code} 형식 처리
        if (uri.Scheme == "milops" && uri.Host == "invite")
        {
            var inviteCode = uri.LastPathSegment;
            if (!string.IsNullOrEmpty(inviteCode))
            {
                Log.Info(TAG, $"Invite code: {inviteCode}");
                // 초대 코드를 앱에 전달 (정적 변수 또는 이벤트 사용)
                DeepLinkHandler.PendingInviteCode = inviteCode;
            }
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
        }
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .LogToTrace();
    }
}

/// <summary>
/// 딥링크 처리를 위한 정적 클래스
/// </summary>
public static class DeepLinkHandler
{
    /// <summary>
    /// 처리 대기 중인 초대 코드
    /// 앱이 초대 링크로 실행된 경우 설정됨
    /// </summary>
    public static string? PendingInviteCode { get; set; }

    /// <summary>
    /// 초대 코드 소비 (한 번 읽으면 null로 리셋)
    /// </summary>
    public static string? ConsumePendingInviteCode()
    {
        var code = PendingInviteCode;
        PendingInviteCode = null;
        return code;
    }

    /// <summary>
    /// 초대 코드가 있는지 확인
    /// </summary>
    public static bool HasPendingInvite => !string.IsNullOrEmpty(PendingInviteCode);
}
