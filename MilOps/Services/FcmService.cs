using System;
using System.Threading.Tasks;
using MilOps.Models;

namespace MilOps.Services;

/// <summary>
/// FCM 토큰 관리 서비스 (플랫폼 독립적)
/// </summary>
public static class FcmService
{
    /// <summary>
    /// FCM 토큰 제공자 (Android에서 설정)
    /// </summary>
    public static Func<Task<string?>>? TokenProvider { get; set; }

    /// <summary>
    /// 현재 FCM 토큰
    /// </summary>
    public static string? CurrentToken { get; set; }

    /// <summary>
    /// FCM 토큰을 서버에 저장
    /// </summary>
    public static async Task<bool> SaveTokenToServerAsync(string? token = null)
    {
        try
        {
            // 토큰이 전달되지 않으면 TokenProvider에서 가져오기
            if (string.IsNullOrEmpty(token))
            {
                if (TokenProvider != null)
                {
                    token = await TokenProvider();
                }
                else
                {
                    token = CurrentToken;
                }
            }

            if (string.IsNullOrEmpty(token))
            {
                System.Diagnostics.Debug.WriteLine("[FcmService] No FCM token available");
                return false;
            }

            // 현재 로그인된 사용자 확인
            if (!AuthService.IsLoggedIn || AuthService.CurrentUser == null)
            {
                System.Diagnostics.Debug.WriteLine("[FcmService] User not logged in, storing token locally");
                CurrentToken = token;
                return false;
            }

            var client = SupabaseService.Client;
            if (client == null)
            {
                System.Diagnostics.Debug.WriteLine("[FcmService] Supabase client not initialized");
                CurrentToken = token;
                return false;
            }

            var userId = AuthService.CurrentUser.Id;

            System.Diagnostics.Debug.WriteLine($"[FcmService] Saving FCM token for user {userId}");

            // 1. 해당 FCM 토큰의 다른 사용자 등록을 모두 비활성화 (디바이스 1개 = 1명만 활성)
            System.Diagnostics.Debug.WriteLine("[FcmService] Deactivating other users' registrations for this token");
#pragma warning disable CS8603
            await client
                .From<UserDevice>()
                .Filter("fcm_token", Supabase.Postgrest.Constants.Operator.Equals, token)
                .Filter("user_id", Supabase.Postgrest.Constants.Operator.NotEqual, userId.ToString())
                .Set(d => d.IsActive, false)
                .Update();
#pragma warning restore CS8603

            // 2. 현재 사용자의 기존 토큰 확인
            var existingDevices = await client
                .From<UserDevice>()
                .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, userId.ToString())
                .Filter("fcm_token", Supabase.Postgrest.Constants.Operator.Equals, token)
                .Get();

            if (existingDevices.Models.Count > 0)
            {
                // 이미 등록된 토큰 - last_used_at만 업데이트
                System.Diagnostics.Debug.WriteLine("[FcmService] Token already exists, updating last_used_at");
#pragma warning disable CS8603
                await client
                    .From<UserDevice>()
                    .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, userId.ToString())
                    .Filter("fcm_token", Supabase.Postgrest.Constants.Operator.Equals, token)
                    .Set(d => d.LastUsedAt, DateTime.UtcNow)
                    .Set(d => d.IsActive, true)
                    .Update();
#pragma warning restore CS8603
            }
            else
            {
                // 새 토큰 등록
                System.Diagnostics.Debug.WriteLine("[FcmService] Registering new FCM token");
                await client
                    .From<UserDevice>()
                    .Insert(new UserDevice
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        FcmToken = token,
                        DeviceType = "android",
                        DeviceName = GetDeviceName(),
                        IsActive = true,
                        LastUsedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    });
            }

            CurrentToken = token;
            System.Diagnostics.Debug.WriteLine("[FcmService] FCM token saved successfully");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FcmService] SaveTokenToServerAsync error: {ex.Message}");
            if (!string.IsNullOrEmpty(token))
            {
                CurrentToken = token;
            }
            return false;
        }
    }

    /// <summary>
    /// 디바이스 토큰 비활성화 (로그아웃 시)
    /// </summary>
    public static async Task DeactivateTokenAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(CurrentToken) || AuthService.CurrentUser == null)
            {
                return;
            }

            var client = SupabaseService.Client;
            if (client == null) return;

#pragma warning disable CS8603
            await client
                .From<UserDevice>()
                .Filter("fcm_token", Supabase.Postgrest.Constants.Operator.Equals, CurrentToken)
                .Set(d => d.IsActive, false)
                .Update();
#pragma warning restore CS8603

            System.Diagnostics.Debug.WriteLine("[FcmService] FCM token deactivated");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FcmService] DeactivateTokenAsync error: {ex.Message}");
        }
    }

    /// <summary>
    /// 디바이스 이름 가져오기 (플랫폼별 구현)
    /// </summary>
    public static Func<string>? DeviceNameProvider { get; set; }

    private static string GetDeviceName()
    {
        if (DeviceNameProvider != null)
        {
            return DeviceNameProvider();
        }
        return "Unknown";
    }
}
