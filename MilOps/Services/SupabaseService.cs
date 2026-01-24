using System;
using System.IO;
using System.Threading.Tasks;
using DotNetEnv;
using Supabase;

namespace MilOps.Services;

public static class SupabaseService
{
    private static Supabase.Client? _client;
    private static bool _initialized = false;

    public static Supabase.Client Client
    {
        get
        {
            if (_client == null)
                throw new InvalidOperationException("Supabase client not initialized. Call InitializeAsync first.");
            return _client;
        }
    }

    public static bool IsInitialized => _initialized;

    public static async Task InitializeAsync()
    {
        if (_initialized) return;

        // .env 파일 로드
        var envPath = Path.Combine(AppContext.BaseDirectory, ".env");
        if (File.Exists(envPath))
        {
            Env.Load(envPath);
        }
        else
        {
            // 개발 환경에서는 프로젝트 루트의 .env 파일 사용
            var devEnvPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".env");
            if (File.Exists(devEnvPath))
            {
                Env.Load(devEnvPath);
            }
        }

        var supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL");
        var supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY");

        if (string.IsNullOrEmpty(supabaseUrl) || string.IsNullOrEmpty(supabaseKey))
        {
            throw new InvalidOperationException(
                "SUPABASE_URL and SUPABASE_ANON_KEY must be set in environment variables or .env file");
        }

        var options = new SupabaseOptions
        {
            AutoRefreshToken = true,
            AutoConnectRealtime = false
        };

        _client = new Supabase.Client(supabaseUrl, supabaseKey, options);
        await _client.InitializeAsync();

        _initialized = true;
    }

    /// <summary>
    /// login_id로 사용자의 email을 조회
    /// </summary>
    public static async Task<string?> GetEmailByLoginIdAsync(string loginId)
    {
        var response = await Client.From<Models.User>()
            .Select("email")
            .Filter("login_id", Supabase.Postgrest.Constants.Operator.Equals, loginId)
            .Single();

        return response?.Email;
    }

    /// <summary>
    /// 현재 로그인한 사용자의 프로필 정보 조회
    /// </summary>
    public static async Task<Models.User?> GetCurrentUserProfileAsync()
    {
        var session = Client.Auth.CurrentSession;
        if (session?.User == null) return null;

        var response = await Client.From<Models.User>()
            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, session.User.Id!)
            .Single();

        return response;
    }
}
