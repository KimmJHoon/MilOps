using MilOps.Models;
using Supabase.Realtime;
using Supabase.Realtime.PostgresChanges;
using EventType = Supabase.Realtime.Constants.EventType;
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
    private static RealtimeChannel? _realtimeChannel;
    private static bool _isSubscribed = false;
    private static bool _isSubscribing = false;
    private static readonly object _subscribeLock = new object();

    /// <summary>
    /// 새 알림 수신 이벤트
    /// </summary>
    public static event Action<Notification>? OnNewNotification;

    /// <summary>
    /// 알림 업데이트 이벤트 (읽음 처리 등)
    /// </summary>
    public static event Action<Notification>? OnNotificationUpdated;

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

    /// <summary>
    /// 현재 구독 중인 사용자 ID
    /// </summary>
    private static Guid _subscribedUserId = Guid.Empty;

    /// <summary>
    /// 실시간 알림 구독 시작
    /// </summary>
    public static async Task SubscribeToRealtimeAsync()
    {
        System.Diagnostics.Debug.WriteLine($"[NotificationService] SubscribeToRealtimeAsync called, _isSubscribed: {_isSubscribed}, _isSubscribing: {_isSubscribing}");

        // 중복 호출 방지
        lock (_subscribeLock)
        {
            if (_isSubscribed || _isSubscribing)
            {
                System.Diagnostics.Debug.WriteLine("[NotificationService] Already subscribed or subscribing, skipping");
                return;
            }
            _isSubscribing = true;
        }

        var currentUser = AuthService.CurrentUser;
        if (currentUser == null)
        {
            System.Diagnostics.Debug.WriteLine("[NotificationService] Cannot subscribe - no current user");
            _isSubscribing = false;
            return;
        }

        // 사용자 ID 저장 (핸들러에서 사용)
        _subscribedUserId = currentUser.Id;

        System.Diagnostics.Debug.WriteLine($"[NotificationService] Current user: {currentUser.Id} ({currentUser.LoginId})");

        try
        {
            var client = SupabaseService.Client;
            if (client?.Realtime == null)
            {
                System.Diagnostics.Debug.WriteLine("[NotificationService] Realtime client not available");
                _isSubscribing = false;
                return;
            }

            System.Diagnostics.Debug.WriteLine("[NotificationService] Connecting to Realtime...");

            // Realtime 연결
            await SupabaseService.ConnectRealtimeAsync();

            System.Diagnostics.Debug.WriteLine("[NotificationService] Realtime connected, creating channel...");

            // 채널 이름에 고유 식별자 추가 (중복 방지)
            var channelName = $"notifications-{currentUser.Id}";
            _realtimeChannel = client.Realtime.Channel(channelName);

            System.Diagnostics.Debug.WriteLine($"[NotificationService] Channel created: {channelName}");

            // PostgresChanges 옵션 설정
            var pgOptions = new PostgresChangesOptions("public", "notifications");

            // 핸들러 등록 - 모든 이벤트 수신
            _realtimeChannel
                .Register(pgOptions)
                .AddPostgresChangeHandler(PostgresChangesOptions.ListenType.All, HandlePostgresChange);

            System.Diagnostics.Debug.WriteLine("[NotificationService] Postgres change handler registered");

            System.Diagnostics.Debug.WriteLine("[NotificationService] Subscribing to channel...");

            await _realtimeChannel.Subscribe();
            _isSubscribed = true;
            _isSubscribing = false;

            System.Diagnostics.Debug.WriteLine($"[NotificationService] Subscribed to realtime notifications for user {currentUser.Id}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationService] SubscribeToRealtimeAsync error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[NotificationService] StackTrace: {ex.StackTrace}");
            _isSubscribed = false;
            _isSubscribing = false;
        }
    }

    /// <summary>
    /// Postgres 변경 이벤트 핸들러
    /// </summary>
    private static void HandlePostgresChange(object sender, PostgresChangesResponse change)
    {
        System.Diagnostics.Debug.WriteLine($"[NotificationService] Postgres change received! Event: {change.Event}, Payload type: {change.Payload?.GetType().Name}");

        try
        {
            var model = change.Model<Notification>();
            if (model == null)
            {
                System.Diagnostics.Debug.WriteLine("[NotificationService] Could not parse notification model");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[NotificationService] Notification: {model.Title}, UserId: {model.UserId}, CurrentUser: {_subscribedUserId}");

            // 현재 사용자의 알림만 처리
            if (model.UserId != _subscribedUserId)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationService] Ignoring - not my notification");
                return;
            }

            // Event 타입으로 INSERT/UPDATE 구분
            // EventType enum: Insert, Update, Delete, PostgresChanges
            var isInsert = change.Event == EventType.Insert || change.Event == EventType.PostgresChanges;
            var isUpdate = change.Event == EventType.Update;

            System.Diagnostics.Debug.WriteLine($"[NotificationService] Event: {change.Event}, IsInsert: {isInsert}, IsUpdate: {isUpdate}");

            if (isInsert)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationService] Invoking OnNewNotification for: {model.Title}");
                OnNewNotification?.Invoke(model);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationService] Invoking OnNotificationUpdated for: {model.Id}");
                OnNotificationUpdated?.Invoke(model);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationService] HandlePostgresChange error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[NotificationService] StackTrace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// 실시간 알림 구독 해제
    /// </summary>
    public static void UnsubscribeFromRealtime()
    {
        if (!_isSubscribed || _realtimeChannel == null)
        {
            return;
        }

        try
        {
            _realtimeChannel.Unsubscribe();
            _realtimeChannel = null;
            _isSubscribed = false;
            _subscribedUserId = Guid.Empty;

            System.Diagnostics.Debug.WriteLine("[NotificationService] Unsubscribed from realtime notifications");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationService] UnsubscribeFromRealtime error: {ex.Message}");
        }
    }

    /// <summary>
    /// 구독 상태 확인
    /// </summary>
    public static bool IsSubscribed => _isSubscribed;
}
