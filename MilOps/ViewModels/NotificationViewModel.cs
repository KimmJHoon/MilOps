using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MilOps.Models;
using MilOps.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace MilOps.ViewModels;

public partial class NotificationViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<Notification> _notifications = new();

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private bool _hasNotifications = false;

    [ObservableProperty]
    private int _unreadCount = 0;

    [ObservableProperty]
    private string _emptyMessage = "알림이 없습니다";

    [ObservableProperty]
    private bool _isRealtimeConnected = false;

    // 일정 상세 화면으로 이동 이벤트
    public event Action<Guid>? OnScheduleSelected;

    // 닫기 이벤트
    public event EventHandler? CloseRequested;

    // 새 알림 수신 이벤트 (UI 업데이트용)
    public event Action<Notification>? OnNewNotificationReceived;

    public NotificationViewModel()
    {
        // 실시간 알림 이벤트 구독
        NotificationService.OnNewNotification += HandleNewNotification;
        NotificationService.OnNotificationUpdated += HandleNotificationUpdated;
    }

    /// <summary>
    /// 새 알림 수신 처리
    /// </summary>
    private void HandleNewNotification(Notification notification)
    {
        // UI 스레드에서 실행
        Dispatcher.UIThread.Post(() =>
        {
            // 목록 맨 앞에 추가
            Notifications.Insert(0, notification);
            HasNotifications = true;
            UnreadCount++;

            System.Diagnostics.Debug.WriteLine($"[NotificationVM] New notification added: {notification.Title}");

            // 외부 알림 (토스트 등)
            OnNewNotificationReceived?.Invoke(notification);
        });
    }

    /// <summary>
    /// 알림 업데이트 처리
    /// </summary>
    private void HandleNotificationUpdated(Notification updatedNotification)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var existing = Notifications.FirstOrDefault(n => n.Id == updatedNotification.Id);
            if (existing != null)
            {
                var index = Notifications.IndexOf(existing);
                if (index >= 0)
                {
                    Notifications[index] = updatedNotification;
                    UnreadCount = Notifications.Count(n => !n.IsRead);

                    System.Diagnostics.Debug.WriteLine($"[NotificationVM] Notification updated: {updatedNotification.Id}");
                }
            }
        });
    }

    /// <summary>
    /// 실시간 구독 시작
    /// </summary>
    public async Task StartRealtimeSubscriptionAsync()
    {
        try
        {
            await NotificationService.SubscribeToRealtimeAsync();
            IsRealtimeConnected = NotificationService.IsSubscribed;
            System.Diagnostics.Debug.WriteLine($"[NotificationVM] Realtime subscription started: {IsRealtimeConnected}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationVM] StartRealtimeSubscriptionAsync error: {ex.Message}");
            IsRealtimeConnected = false;
        }
    }

    /// <summary>
    /// 실시간 구독 중지
    /// </summary>
    public void StopRealtimeSubscription()
    {
        try
        {
            NotificationService.UnsubscribeFromRealtime();
            IsRealtimeConnected = false;
            System.Diagnostics.Debug.WriteLine("[NotificationVM] Realtime subscription stopped");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationVM] StopRealtimeSubscription error: {ex.Message}");
        }
    }

    /// <summary>
    /// ViewModel 정리 (구독 해제)
    /// </summary>
    public void Cleanup()
    {
        NotificationService.OnNewNotification -= HandleNewNotification;
        NotificationService.OnNotificationUpdated -= HandleNotificationUpdated;
        StopRealtimeSubscription();
    }

    /// <summary>
    /// 알림 목록 로드
    /// </summary>
    public async Task LoadNotificationsAsync()
    {
        IsLoading = true;

        try
        {
            var notifications = await NotificationService.GetNotificationsAsync();

            Notifications.Clear();
            foreach (var notification in notifications)
            {
                Notifications.Add(notification);
            }

            HasNotifications = Notifications.Any();
            UnreadCount = Notifications.Count(n => !n.IsRead);

            System.Diagnostics.Debug.WriteLine($"[NotificationVM] Loaded {Notifications.Count} notifications, {UnreadCount} unread");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationVM] LoadNotificationsAsync error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 읽지 않은 알림 개수만 로드
    /// </summary>
    public async Task LoadUnreadCountAsync()
    {
        try
        {
            UnreadCount = await NotificationService.GetUnreadCountAsync();
            System.Diagnostics.Debug.WriteLine($"[NotificationVM] Unread count: {UnreadCount}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationVM] LoadUnreadCountAsync error: {ex.Message}");
        }
    }

    /// <summary>
    /// 알림 클릭 - 읽음 처리 후 일정으로 이동
    /// </summary>
    [RelayCommand]
    private async Task SelectNotificationAsync(Notification? notification)
    {
        if (notification == null) return;

        // 읽음 처리
        if (!notification.IsRead)
        {
            var success = await NotificationService.MarkAsReadAsync(notification.Id);
            if (success)
            {
                notification.IsRead = true;
                UnreadCount = Math.Max(0, UnreadCount - 1);

                // UI 갱신을 위해 컬렉션 업데이트
                var index = Notifications.IndexOf(notification);
                if (index >= 0)
                {
                    Notifications[index] = notification;
                }
            }
        }

        // 일정으로 이동
        if (notification.ScheduleId.HasValue)
        {
            OnScheduleSelected?.Invoke(notification.ScheduleId.Value);
        }
    }

    /// <summary>
    /// 모든 알림 읽음 처리
    /// </summary>
    [RelayCommand]
    private async Task MarkAllAsReadAsync()
    {
        if (UnreadCount == 0) return;

        IsLoading = true;

        try
        {
            var success = await NotificationService.MarkAllAsReadAsync();
            if (success)
            {
                foreach (var notification in Notifications)
                {
                    notification.IsRead = true;
                }
                UnreadCount = 0;

                // UI 갱신
                await LoadNotificationsAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationVM] MarkAllAsReadAsync error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 새로고침
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadNotificationsAsync();
    }

    /// <summary>
    /// 닫기
    /// </summary>
    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
