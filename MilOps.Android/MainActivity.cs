using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using Avalonia;
using Avalonia.Android;
using MilOps.Config;
using System;
using System.IO;

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
            // Android Assets에서 .env 파일 읽어서 SupabaseConfig에 설정
            LoadSupabaseConfig();

            base.OnCreate(savedInstanceState);
        }
        catch (Exception ex)
        {
            Log.Error("MilOps", $"OnCreate Error: {ex.Message}");
            Log.Error("MilOps", $"StackTrace: {ex.StackTrace}");
            throw;
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
