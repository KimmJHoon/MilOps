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
    }

    /// <summary>
    /// 세션 정보 저장
    /// </summary>
    public static async Task SaveSessionAsync(string accessToken, string refreshToken, string loginId)
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
                LoginId = loginId
            };

            var json = JsonSerializer.Serialize(sessionData);
            await File.WriteAllTextAsync(SessionFilePath, json);
            System.Diagnostics.Debug.WriteLine($"[SessionStorage] Session saved for {loginId}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SessionStorage] Failed to save session: {ex.Message}");
        }
    }

    /// <summary>
    /// 저장된 세션 정보 로드
    /// </summary>
    public static async Task<(string? accessToken, string? refreshToken, string? loginId)> LoadSessionAsync()
    {
        try
        {
            if (!File.Exists(SessionFilePath))
            {
                System.Diagnostics.Debug.WriteLine("[SessionStorage] No saved session found");
                return (null, null, null);
            }

            var json = await File.ReadAllTextAsync(SessionFilePath);
            var sessionData = JsonSerializer.Deserialize<SessionData>(json);

            if (sessionData != null)
            {
                System.Diagnostics.Debug.WriteLine($"[SessionStorage] Session loaded for {sessionData.LoginId}");
                return (sessionData.AccessToken, sessionData.RefreshToken, sessionData.LoginId);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SessionStorage] Failed to load session: {ex.Message}");
        }

        return (null, null, null);
    }

    /// <summary>
    /// 저장된 세션 삭제
    /// </summary>
    public static void ClearSession()
    {
        try
        {
            if (File.Exists(SessionFilePath))
            {
                File.Delete(SessionFilePath);
                System.Diagnostics.Debug.WriteLine("[SessionStorage] Session cleared");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SessionStorage] Failed to clear session: {ex.Message}");
        }
    }
}
