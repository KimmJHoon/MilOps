using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using Avalonia;
using Avalonia.Android;
using System;

namespace MilOps.Android;

[Activity(
    Label = "@string/app_name",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        try
        {
            base.OnCreate(savedInstanceState);
        }
        catch (Exception ex)
        {
            Log.Error("MilOps", $"OnCreate Error: {ex.Message}");
            Log.Error("MilOps", $"StackTrace: {ex.StackTrace}");
            throw;
        }
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .LogToTrace();
    }
}
