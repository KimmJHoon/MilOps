using MilOps.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MilOps.Services;

/// <summary>
/// 알림 서비스
/// </summary>
public static class NotificationService
{
    /// <summary>
    /// 현재 사용자의 알림 목록 조회
    /// </summary>
    public static async Task<List<Notification>> GetNotificationsAsync(int limit = 50)
    {
        try
        {
            var currentUser = AuthService.CurrentUser;
            if (currentUser == null) return new List<Notification>();

            var client = SupabaseService.Client;
            if (client == null) return new List<Notification>();

            var response = await client.From<Notification>()
                .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, currentUser.Id.ToString())
                .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                .Limit(limit)
                .Get();

            return response.Models.ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationService] GetNotificationsAsync error: {ex.Message}");
            return new List<Notification>();
        }
    }

    /// <summary>
    /// 읽지 않은 알림 개수 조회
    /// </summary>
    public static async Task<int> GetUnreadCountAsync()
    {
        try
        {
            var currentUser = AuthService.CurrentUser;
            if (currentUser == null) return 0;

            var client = SupabaseService.Client;
            if (client == null) return 0;

            var response = await client.From<Notification>()
                .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, currentUser.Id.ToString())
                .Filter("is_read", Supabase.Postgrest.Constants.Operator.Equals, "false")
                .Get();

            return response.Models.Count;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationService] GetUnreadCountAsync error: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// 알림 읽음 처리
    /// </summary>
    public static async Task<bool> MarkAsReadAsync(Guid notificationId)
    {
        try
        {
            var client = SupabaseService.Client;
            if (client == null) return false;

#pragma warning disable CS8603
            await client.From<Notification>()
                .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, notificationId.ToString())
                .Set(n => n.IsRead, true)
                .Set(n => n.ReadAt, DateTime.UtcNow)
                .Update();
#pragma warning restore CS8603

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationService] MarkAsReadAsync error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 모든 알림 읽음 처리
    /// </summary>
    public static async Task<bool> MarkAllAsReadAsync()
    {
        try
        {
            var currentUser = AuthService.CurrentUser;
            if (currentUser == null) return false;

            var client = SupabaseService.Client;
            if (client == null) return false;

#pragma warning disable CS8603
            await client.From<Notification>()
                .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, currentUser.Id.ToString())
                .Filter("is_read", Supabase.Postgrest.Constants.Operator.Equals, "false")
                .Set(n => n.IsRead, true)
                .Set(n => n.ReadAt, DateTime.UtcNow)
                .Update();
#pragma warning restore CS8603

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationService] MarkAllAsReadAsync error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 알림 생성 (클라이언트에서 직접 호출 - 테스트용)
    /// 실제로는 DB Trigger에서 자동 생성됨
    /// </summary>
    public static async Task<bool> CreateNotificationAsync(Guid userId, string type, string title, string message, Guid? scheduleId = null)
    {
        try
        {
            var client = SupabaseService.Client;
            if (client == null) return false;

            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ScheduleId = scheduleId,
                Type = type,
                Title = title,
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            await client.From<Notification>().Insert(notification);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationService] CreateNotificationAsync error: {ex.Message}");
            return false;
        }
    }
}
