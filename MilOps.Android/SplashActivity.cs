using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
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
    protected override void OnCreate(Bundle? savedInstanceState)
    {
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

        // 1.5초 후 MainActivity로 이동
        new Handler(Looper.MainLooper!).PostDelayed(StartMainActivity, 1500);
    }

    private void StartMainActivity()
    {
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
