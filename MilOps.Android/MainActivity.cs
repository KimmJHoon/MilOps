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

            // Android Assets에서 .env 파일 읽어서 SupabaseConfig에 설정
            LoadSupabaseConfig();

            // Firebase 초기화
            InitializeFirebase();

            // 앱 재시작 서비스 설정
            SetupAppRestartService();

            base.OnCreate(savedInstanceState);

            // 시스템 뒤로가기 버튼 핸들러 등록 (API 33+ 대응)
            OnBackPressedDispatcher.AddCallback(this, new BackPressedCallback(this));

            // Android 13+ 알림 권한 요청
            RequestNotificationPermission();

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
