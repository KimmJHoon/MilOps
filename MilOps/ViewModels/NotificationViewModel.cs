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

    // 채팅 탭으로 이동 이벤트
    public event EventHandler? OnChatSelected;

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
    /// 알림 목록 로드 (스마트 업데이트: 변경분만 반영)
    /// </summary>
    public async Task LoadNotificationsAsync()
    {
        // 첫 로드 시에만 로딩 표시 (이미 데이터가 있으면 백그라운드 갱신)
        bool showLoading = !Notifications.Any();
        if (showLoading) IsLoading = true;

        try
        {
            var notifications = await NotificationService.GetNotificationsAsync();

            // 기존 데이터가 없으면 한 번에 교체
            if (!Notifications.Any())
            {
                foreach (var notification in notifications)
                {
                    Notifications.Add(notification);
                }
            }
            else
            {
                // 스마트 업데이트: ID 기반으로 변경분만 반영
                var newIds = notifications.Select(n => n.Id).ToHashSet();
                var existingIds = Notifications.Select(n => n.Id).ToHashSet();

                // 삭제된 항목 제거 (역순으로 순회)
                for (int i = Notifications.Count - 1; i >= 0; i--)
                {
                    if (!newIds.Contains(Notifications[i].Id))
                    {
                        Notifications.RemoveAt(i);
                    }
                }

                // 새 항목 추가 및 기존 항목 업데이트
                for (int i = 0; i < notifications.Count; i++)
                {
                    var newItem = notifications[i];
                    if (i < Notifications.Count)
                    {
                        if (Notifications[i].Id != newItem.Id)
                        {
                            // 위치가 바뀐 경우 삽입
                            Notifications.Insert(i, newItem);
                        }
                        else if (Notifications[i].IsRead != newItem.IsRead)
                        {
                            // 읽음 상태 변경 시 교체
                            Notifications[i] = newItem;
                        }
                    }
                    else
                    {
                        // 새 항목 추가
                        Notifications.Add(newItem);
                    }
                }

                // 길이 초과분 제거
                while (Notifications.Count > notifications.Count)
                {
                    Notifications.RemoveAt(Notifications.Count - 1);
                }
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

                // 헤더 뱃지 갱신 이벤트 발생 (1개 읽음)
                NotificationService.RaiseUnreadCountChanged(-1);
            }
        }

        // 타입에 따라 화면 이동
        if (notification.Type == "chat_message")
        {
            // 채팅 탭으로 이동
            OnChatSelected?.Invoke(this, EventArgs.Empty);
        }
        else if (notification.ScheduleId.HasValue)
        {
            // 일정으로 이동
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
            var previousUnread = UnreadCount;
            var success = await NotificationService.MarkAllAsReadAsync();
            if (success)
            {
                // UI 즉시 반영: 전체 reload 대신 로컬 상태만 업데이트
                for (int i = 0; i < Notifications.Count; i++)
                {
                    if (!Notifications[i].IsRead)
                    {
                        var updated = Notifications[i];
                        updated.IsRead = true;
                        Notifications[i] = updated; // 컬렉션 변경 알림 트리거
                    }
                }
                UnreadCount = 0;

                // 헤더 뱃지 갱신 이벤트 발생 (전체 읽음 → 0으로 리셋)
                if (previousUnread > 0)
                {
                    NotificationService.RaiseUnreadCountChanged(-previousUnread);
                }
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
