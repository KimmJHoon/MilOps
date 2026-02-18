using System;

namespace MilOps.Services;

/// <summary>
/// 딥링크/푸시 알림 탭 네비게이션 서비스
/// Android/iOS에서 알림 탭 시 Avalonia UI로 네비게이션 요청 전달
/// </summary>
public static class DeepLinkService
{
    /// <summary>
    /// 네비게이션 요청 이벤트 (MainView에서 구독)
    /// </summary>
    public static event Action<DeepLinkArgs>? NavigationRequested;

    /// <summary>
    /// 대기 중인 딥링크 (앱이 아직 초기화되지 않았을 때 저장)
    /// MainView 초기화 시 확인 후 처리
    /// </summary>
    public static DeepLinkArgs? PendingNavigation { get; set; }

    /// <summary>
    /// 네비게이션 요청 발생
    /// </summary>
    public static void RequestNavigation(string targetTab, string? type = null, Guid? scheduleId = null)
    {
        var args = new DeepLinkArgs
        {
            TargetTab = targetTab,
            Type = type,
            ScheduleId = scheduleId
        };

        // 이벤트 핸들러가 등록되어 있으면 즉시 발행
        if (NavigationRequested != null)
        {
            NavigationRequested.Invoke(args);
        }
        else
        {
            // 아직 UI가 준비되지 않았으면 대기열에 저장
            PendingNavigation = args;
        }
    }

    /// <summary>
    /// 대기 중인 딥링크 처리 후 클리어
    /// </summary>
    public static DeepLinkArgs? ConsumePending()
    {
        var pending = PendingNavigation;
        PendingNavigation = null;
        return pending;
    }
}

/// <summary>
/// 딥링크 네비게이션 인자
/// </summary>
public class DeepLinkArgs
{
    /// <summary>
    /// 이동할 탭 이름: "calendar", "schedule", "chat", "notification", "manager"
    /// </summary>
    public string TargetTab { get; set; } = "calendar";

    /// <summary>
    /// 알림 유형: "chat_message", "schedule_confirmed", etc.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// 일정 ID (일정 관련 알림인 경우)
    /// </summary>
    public Guid? ScheduleId { get; set; }
}
