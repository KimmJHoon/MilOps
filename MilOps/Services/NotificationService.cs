using MilOps.Config;
using MilOps.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MilOps.Services;

/// <summary>
/// 알림 서비스
/// DB 알림 생성 + FCM 푸시 알림 발송
/// </summary>
public static class NotificationService
{
    private static readonly HttpClient _httpClient = new();

    /// <summary>
    /// 읽지않은 알림 개수 변경 이벤트 (delta: 음수면 읽은 만큼 차감, 양수면 새 알림)
    /// MainViewModel에서 구독하여 헤더 뱃지 업데이트
    /// </summary>
    public static event Action<int>? UnreadCountChanged;

    /// <summary>
    /// UnreadCountChanged 이벤트 발생 (ViewModel에서 호출)
    /// </summary>
    public static void RaiseUnreadCountChanged(int delta)
    {
        UnreadCountChanged?.Invoke(delta);
    }

    /// <summary>
    /// 현재 사용자의 알림 목록 조회
    /// </summary>
    public static async Task<List<Notification>> GetNotificationsAsync(int limit = 50)
    {
        var sw = Stopwatch.StartNew();
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

            Debug.WriteLine($"[PERF][Notification] GetNotifications ({response.Models.Count}건): {sw.ElapsedMilliseconds}ms");
            return response.Models.ToList();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NotificationService] GetNotificationsAsync error: {ex.Message}");
            return new List<Notification>();
        }
    }

    /// <summary>
    /// 읽지 않은 알림 개수 조회 (id만 SELECT하여 전송량 최소화)
    /// </summary>
    public static async Task<int> GetUnreadCountAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var currentUser = AuthService.CurrentUser;
            if (currentUser == null) return 0;

            var client = SupabaseService.Client;
            if (client == null) return 0;

            // id 컬럼만 SELECT하여 네트워크 전송량 최소화
            var response = await client.From<Notification>()
                .Select("id")
                .Filter("user_id", Supabase.Postgrest.Constants.Operator.Equals, currentUser.Id.ToString())
                .Filter("is_read", Supabase.Postgrest.Constants.Operator.Equals, "false")
                .Get();

            Debug.WriteLine($"[PERF][Notification] GetUnreadCount ({response.Models.Count}건): {sw.ElapsedMilliseconds}ms");
            return response.Models.Count;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NotificationService] GetUnreadCountAsync error: {ex.Message}");
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
    /// 알림 생성 (DB 저장 + FCM 푸시 발송)
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
            Debug.WriteLine($"[NotificationService] DB notification created: type={type}, userId={userId}");

            // FCM 푸시 알림 발송 (fire-and-forget, 실패해도 DB 알림은 이미 저장됨)
            _ = Task.Run(() => SendFcmPushAsync(userId, type, title, message, scheduleId));

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationService] CreateNotificationAsync error: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// FCM 푸시 알림 발송 (send-fcm Edge Function 호출)
    /// </summary>
    private static async Task SendFcmPushAsync(Guid userId, string type, string title, string message, Guid? scheduleId)
    {
        try
        {
            var supabaseUrl = SupabaseConfig.Url;
            var supabaseKey = SupabaseConfig.AnonKey;

            if (string.IsNullOrEmpty(supabaseUrl) || string.IsNullOrEmpty(supabaseKey))
            {
                Debug.WriteLine("[NotificationService] FCM push skipped: Supabase config missing");
                return;
            }

            var payload = new Dictionary<string, object?>
            {
                { "user_id", userId.ToString() },
                { "title", title },
                { "body", message },
                { "type", type },
                { "schedule_id", scheduleId?.ToString() }
            };

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, $"{supabaseUrl}/functions/v1/send-fcm")
            {
                Content = content
            };
            request.Headers.Add("Authorization", $"Bearer {supabaseKey}");

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[NotificationService] FCM push sent: type={type}, userId={userId}");
            }
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[NotificationService] FCM push failed: {response.StatusCode} - {responseBody}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NotificationService] SendFcmPushAsync error: {ex.Message}");
        }
    }

    /// <summary>
    /// super_admin (행정안전부 + 제2작전사) 전원에게 알림 전송
    /// 일정 생성/입력/예약/확정 시 호출 — 백그라운드 fire-and-forget
    /// </summary>
    public static async Task NotifySuperAdminsAsync(string type, string title, string message, Guid? scheduleId = null)
    {
        try
        {
            var client = SupabaseService.Client;
            if (client == null) return;

            var currentUser = AuthService.CurrentUser;
            if (currentUser == null) return;

            // super_admin 유저 조회 (행정안전부 + 제2작전사)
            var moisResponse = await client.From<User>()
                .Filter("role", Supabase.Postgrest.Constants.Operator.Equals, "super_admin_mois")
                .Filter("is_active", Supabase.Postgrest.Constants.Operator.Equals, "true")
                .Get();

            var armyResponse = await client.From<User>()
                .Filter("role", Supabase.Postgrest.Constants.Operator.Equals, "super_admin_army")
                .Filter("is_active", Supabase.Postgrest.Constants.Operator.Equals, "true")
                .Get();

            var superAdmins = moisResponse.Models.Concat(armyResponse.Models)
                .Where(u => u.Id != currentUser.Id) // 자기 자신은 제외
                .ToList();

            Debug.WriteLine($"[Notification] super_admin {superAdmins.Count}명에게 알림 전송: {type}");

            // 각 super_admin에게 알림 생성
            foreach (var admin in superAdmins)
            {
                await CreateNotificationAsync(admin.Id, type, title, message, scheduleId);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NotificationService] NotifySuperAdminsAsync error: {ex.Message}");
        }
    }

}
