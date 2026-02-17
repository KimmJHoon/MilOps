using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace MilOps.Services;

/// <summary>
/// 세션 토큰을 로컬 파일에 저장/복원하는 서비스
/// </summary>
public static class SessionStorageService
{
    private static readonly string SessionFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MilOps",
        "session.json"
    );

    private class SessionData
    {
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public string? LoginId { get; set; }
        public string? CachedEmail { get; set; }
    }

    /// <summary>
    /// 세션 정보 저장
    /// </summary>
    public static async Task SaveSessionAsync(string accessToken, string refreshToken, string loginId, string? email = null)
    {
        try
        {
            var directory = Path.GetDirectoryName(SessionFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var sessionData = new SessionData
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                LoginId = loginId,
                CachedEmail = email
            };

            var json = JsonSerializer.Serialize(sessionData);
            await File.WriteAllTextAsync(SessionFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SessionStorage] Failed to save session: {ex.Message}");
        }
    }

    /// <summary>
    /// 저장된 세션 정보 로드
    /// </summary>
    public static async Task<(string? accessToken, string? refreshToken, string? loginId, string? cachedEmail)> LoadSessionAsync()
    {
        try
        {
            if (!File.Exists(SessionFilePath))
            {
                return (null, null, null, null);
            }

            var json = await File.ReadAllTextAsync(SessionFilePath);
            var sessionData = JsonSerializer.Deserialize<SessionData>(json);

            if (sessionData != null)
            {
                return (sessionData.AccessToken, sessionData.RefreshToken, sessionData.LoginId, sessionData.CachedEmail);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SessionStorage] Failed to load session: {ex.Message}");
        }

        return (null, null, null, null);
    }

    /// <summary>
    /// 저장된 세션 삭제 (토큰만 제거, loginId+email 캐시는 보존)
    /// → 로그아웃 후 재로그인 시 email 캐시로 GetUserByLoginId 스킵 가능
    /// </summary>
    public static void ClearSession()
    {
        try
        {
            if (!File.Exists(SessionFilePath)) return;

            // 기존 데이터에서 loginId + email만 보존
            var json = File.ReadAllText(SessionFilePath);
            var existing = JsonSerializer.Deserialize<SessionData>(json);

            if (existing != null && (!string.IsNullOrEmpty(existing.LoginId) || !string.IsNullOrEmpty(existing.CachedEmail)))
            {
                // 토큰만 지우고 loginId+email 보존
                var preserved = new SessionData
                {
                    AccessToken = null,
                    RefreshToken = null,
                    LoginId = existing.LoginId,
                    CachedEmail = existing.CachedEmail
                };
                File.WriteAllText(SessionFilePath, JsonSerializer.Serialize(preserved));
            }
            else
            {
                File.Delete(SessionFilePath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SessionStorage] Failed to clear session: {ex.Message}");
        }
    }
}
