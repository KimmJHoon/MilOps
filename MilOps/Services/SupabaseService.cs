using System;
using System.IO;
using System.Threading.Tasks;
using DotNetEnv;
using MilOps.Config;
using Supabase;

namespace MilOps.Services;

public static class SupabaseService
{
    private static Supabase.Client? _client;
    private static bool _initialized = false;

    /// <summary>
    /// Android에서 Assets를 읽기 위한 델리게이트
    /// </summary>
    public static Func<string, Stream?>? AssetStreamProvider { get; set; }

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

        string? supabaseUrl = null;
        string? supabaseKey = null;

        // 1. SupabaseConfig에서 먼저 확인 (Android 등 플랫폼별 설정)
        if (!string.IsNullOrEmpty(SupabaseConfig.Url) && !string.IsNullOrEmpty(SupabaseConfig.AnonKey))
        {
            supabaseUrl = SupabaseConfig.Url;
            supabaseKey = SupabaseConfig.AnonKey;
        }

        // 2. 환경변수에서 확인
        if (string.IsNullOrEmpty(supabaseUrl) || string.IsNullOrEmpty(supabaseKey))
        {
            supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL");
            supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY");
            // already loaded from env vars
        }

        // 3. 데스크탑: 파일 시스템에서 .env 로드
        if (string.IsNullOrEmpty(supabaseUrl) || string.IsNullOrEmpty(supabaseKey))
        {
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

            supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL");
            supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY");
        }

        if (string.IsNullOrEmpty(supabaseUrl) || string.IsNullOrEmpty(supabaseKey))
        {
            throw new InvalidOperationException(
                "SUPABASE_URL and SUPABASE_ANON_KEY must be set in SupabaseConfig, environment variables, or .env file");
        }

        var options = new SupabaseOptions
        {
            AutoRefreshToken = true,
            AutoConnectRealtime = true  // Realtime 활성화
        };

        _client = new Supabase.Client(supabaseUrl, supabaseKey, options);
        await _client.InitializeAsync();

        _initialized = true;
    }

    /// <summary>
    /// Realtime 연결
    /// </summary>
    public static async Task ConnectRealtimeAsync()
    {
        if (_client?.Realtime != null)
        {
            await _client.Realtime.ConnectAsync();
        }
    }

    /// <summary>
    /// login_id로 사용자의 email을 조회
    /// </summary>
    public static async Task<string?> GetEmailByLoginIdAsync(string loginId)
    {
        try
        {
            var response = await Client.From<Models.User>()
                .Select("email")
                .Filter("login_id", Supabase.Postgrest.Constants.Operator.Equals, loginId)
                .Single();

            return response?.Email;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SupabaseService] GetEmailByLoginIdAsync Error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// login_id로 전체 사용자 정보를 조회
    /// </summary>
    public static async Task<Models.User?> GetUserByLoginIdAsync(string loginId)
    {
        try
        {
            var response = await Client.From<Models.User>()
                .Filter("login_id", Supabase.Postgrest.Constants.Operator.Equals, loginId)
                .Single();

            return response;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SupabaseService] GetUserByLoginIdAsync Error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 현재 로그인한 사용자의 프로필 정보 조회
    /// </summary>
    public static async Task<Models.User?> GetCurrentUserProfileAsync()
    {
        try
        {
            var session = Client.Auth.CurrentSession;
            if (session?.User == null)
            {
                return null;
            }

            var userEmail = session.User.Email;

            // email로 조회 (RLS 정책과 관계없이 작동)
            var response = await Client.From<Models.User>()
                .Filter("email", Supabase.Postgrest.Constants.Operator.Equals, userEmail!)
                .Single();

            if (response == null)
            {
                // email로 못 찾으면 id로 시도
                response = await Client.From<Models.User>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, session.User.Id!)
                    .Single();
            }

            return response;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SupabaseService] GetCurrentUserProfileAsync Error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// .env 파일 내용을 파싱하여 URL과 Key를 추출
    /// </summary>
    private static void ParseEnvContent(string content, out string? supabaseUrl, out string? supabaseKey)
    {
        supabaseUrl = null;
        supabaseKey = null;

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
                supabaseUrl = value;
            else if (key == "SUPABASE_ANON_KEY")
                supabaseKey = value;
        }
    }
}
