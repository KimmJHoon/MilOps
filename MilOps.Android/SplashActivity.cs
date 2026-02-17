using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
using MilOps.Config;
using MilOps.Services;
using System.IO;
using System.Threading;
using SplashScreenCompat = AndroidX.Core.SplashScreen.SplashScreen;

namespace MilOps.Android;

[Activity(
    Label = "@string/app_name",
    Theme = "@style/MyTheme.Splash",
    Icon = "@drawable/icon",
    MainLauncher = true,
    NoHistory = true,
    ScreenOrientation = ScreenOrientation.Unspecified,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class SplashActivity : Activity
{
    // 최소 표시 시간 (애니메이션이 자연스럽게 보이도록)
    private const int MinSplashMs = 800;
    // 최대 대기 시간 (프리페치가 느려도 이 시간 안에 전환)
    private const int MaxSplashMs = 1500;

    private int _preloadDone = 0;    // Interlocked 플래그
    private long _startTicks;         // 스플래시 시작 시각

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        _startTicks = System.Diagnostics.Stopwatch.GetTimestamp();

        // 시스템 스플래시를 즉시 닫기 (Android 12+ 대응)
        var splashScreen = SplashScreenCompat.InstallSplashScreen(this);
        splashScreen.SetKeepOnScreenCondition(new KeepOnScreenFalse());

        base.OnCreate(savedInstanceState);

        SetContentView(Resource.Layout.splash_layout);

        // 페이드인 애니메이션 적용
        var icon = FindViewById<ImageView>(Resource.Id.splash_icon);
        var title = FindViewById<TextView>(Resource.Id.splash_title);
        var subtitle = FindViewById<TextView>(Resource.Id.splash_subtitle);

        var fadeIn = new AlphaAnimation(0f, 1f)
        {
            Duration = 600,
            FillAfter = true
        };

        var slideUp = new TranslateAnimation(
            global::Android.Views.Animations.Dimension.RelativeToSelf, 0f,
            global::Android.Views.Animations.Dimension.RelativeToSelf, 0f,
            global::Android.Views.Animations.Dimension.RelativeToSelf, 0.1f,
            global::Android.Views.Animations.Dimension.RelativeToSelf, 0f)
        {
            Duration = 600,
            FillAfter = true
        };

        var iconAnim = new AnimationSet(true);
        iconAnim.AddAnimation(fadeIn);
        iconAnim.AddAnimation(slideUp);

        icon?.StartAnimation(iconAnim);

        var textFadeIn = new AlphaAnimation(0f, 1f)
        {
            Duration = 600,
            StartOffset = 300,
            FillAfter = true
        };

        title?.StartAnimation(textFadeIn);

        var subtitleFadeIn = new AlphaAnimation(0f, 1f)
        {
            Duration = 600,
            StartOffset = 500,
            FillAfter = true
        };

        subtitle?.StartAnimation(subtitleFadeIn);

        // === 스플래시 대기 시간 동안 프리페치 시작 ===
        // 1. Supabase config 미리 로드 (동기, 매우 빠름 ~1ms)
        PreloadSupabaseConfig();

        // 2. 파일 기반 조직 캐시 미리 메모리에 로드 → 완료 시 즉시 전환 트리거
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                QueryHelper.PreloadOrgData();
                Log.Info("MilOps", "[PERF][Splash] 조직 캐시 Preload 완료");
            }
            catch (System.Exception ex)
            {
                Log.Warn("MilOps", $"[Splash] Preload error (무시): {ex.Message}");
            }
            finally
            {
                // 프리페치 완료 → 최소 시간 경과 확인 후 전환
                Interlocked.Exchange(ref _preloadDone, 1);
                TryStartMainActivity();
            }
        });

        // MaxSplashMs 후 무조건 전환 (프리페치가 느린 경우 안전장치)
        new Handler(Looper.MainLooper!).PostDelayed(() =>
        {
            TryStartMainActivity();
        }, MaxSplashMs);
    }

    /// <summary>
    /// 프리페치 완료 + 최소 표시 시간 경과 시 MainActivity로 전환
    /// 중복 호출 방지: StartMainActivity 내부에서 IsFinishing 체크
    /// </summary>
    private void TryStartMainActivity()
    {
        if (IsFinishing) return;

        var elapsedMs = (System.Diagnostics.Stopwatch.GetTimestamp() - _startTicks)
                        * 1000 / System.Diagnostics.Stopwatch.Frequency;

        if (_preloadDone == 1 && elapsedMs >= MinSplashMs)
        {
            // 최소 시간 경과 + 프리페치 완료 → 즉시 전환
            RunOnUiThread(StartMainActivity);
        }
        else if (_preloadDone == 1 && elapsedMs < MinSplashMs)
        {
            // 프리페치는 끝났지만 최소 시간 미달 → 남은 시간만 대기
            var remaining = MinSplashMs - (int)elapsedMs;
            new Handler(Looper.MainLooper!).PostDelayed(StartMainActivity, remaining);
        }
        // else: 프리페치 미완료 → MaxSplashMs 타이머가 처리
    }

    /// <summary>
    /// Supabase config를 스플래시 단계에서 미리 로드
    /// MainActivity에서도 LoadSupabaseConfig()를 호출하지만 이미 설정되어 있으면 빠르게 스킵됨
    /// </summary>
    private void PreloadSupabaseConfig()
    {
        try
        {
            if (!string.IsNullOrEmpty(SupabaseConfig.Url)) return; // 이미 로드됨

            using var stream = Assets?.Open("config.env");
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                var content = reader.ReadToEnd();
                var lines = content.Split('\n', '\r');
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;
                    var parts = trimmed.Split('=', 2);
                    if (parts.Length != 2) continue;
                    var key = parts[0].Trim();
                    var value = parts[1].Trim().Trim('"');

                    if (key == "SUPABASE_URL") SupabaseConfig.Url = value;
                    else if (key == "SUPABASE_ANON_KEY") SupabaseConfig.AnonKey = value;
                    else if (key == "CHAT_SERVER_HOST") ChatServerConfig.Host = value;
                    else if (key == "CHAT_SERVER_PORT" && int.TryParse(value, out var port)) ChatServerConfig.Port = port;
                }
                Log.Info("MilOps", "[PERF][Splash] Supabase config preloaded");
            }
        }
        catch (System.Exception ex)
        {
            Log.Warn("MilOps", $"[Splash] Config preload failed (무시): {ex.Message}");
        }
    }

    private void StartMainActivity()
    {
        if (IsFinishing) return;  // 중복 전환 방지

        var elapsedMs = (System.Diagnostics.Stopwatch.GetTimestamp() - _startTicks)
                        * 1000 / System.Diagnostics.Stopwatch.Frequency;
        Log.Info("MilOps", $"[PERF][Splash] 총 스플래시 시간: {elapsedMs}ms (프리페치 완료: {_preloadDone == 1})");

        var intent = new Intent(this, typeof(MainActivity));
        intent.AddFlags(ActivityFlags.ClearTop | ActivityFlags.NewTask);
        StartActivity(intent);
        Finish();

        // 전환 애니메이션 (페이드)
        if (Build.VERSION.SdkInt >= BuildVersionCodes.UpsideDownCake)
        {
#pragma warning disable CA1416
            OverrideActivityTransition(OverrideTransition.Open,
                global::Android.Resource.Animation.FadeIn,
                global::Android.Resource.Animation.FadeOut);
#pragma warning restore CA1416
        }
        else
        {
#pragma warning disable CS0618
            OverridePendingTransition(
                global::Android.Resource.Animation.FadeIn,
                global::Android.Resource.Animation.FadeOut);
#pragma warning restore CS0618
        }
    }

    /// <summary>
    /// 시스템 스플래시를 즉시 닫기 위한 콜백
    /// </summary>
    private class KeepOnScreenFalse : Java.Lang.Object, SplashScreenCompat.IKeepOnScreenCondition
    {
        public bool ShouldKeepOnScreen() => false;
    }
}
