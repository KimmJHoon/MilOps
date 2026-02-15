using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MilOps.Models;
using MilOps.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace MilOps.ViewModels;

/// <summary>
/// 알림 ViewModel
/// FCM이 푸시 알림을 처리하므로 Realtime 구독 제거됨
/// </summary>
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

    // 일정 상세 화면으로 이동 이벤트
    public event Action<Guid>? OnScheduleSelected;

    // 닫기 이벤트
    public event EventHandler? CloseRequested;

    public NotificationViewModel()
    {
        // FCM이 푸시 알림을 처리하므로 Realtime 구독 불필요
    }

    /// <summary>
    /// 캐시 초기화 (로그인 전환 시 호출)
    /// </summary>
    public void ClearCache()
    {
        Notifications.Clear();
        HasNotifications = false;
        UnreadCount = 0;
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
