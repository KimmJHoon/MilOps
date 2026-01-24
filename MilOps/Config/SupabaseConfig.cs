namespace MilOps.Config;

/// <summary>
/// Supabase 설정을 관리하는 클래스
/// 플랫폼별로 다른 방식으로 값을 설정할 수 있음
/// </summary>
public static class SupabaseConfig
{
    public static string? Url { get; set; }
    public static string? AnonKey { get; set; }
}
