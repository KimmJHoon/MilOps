using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace MilOps.Models;

/// <summary>
/// 대화 목록 표시용 DTO (get_conversations RPC 결과 매핑)
/// INotifyPropertyChanged 구현으로 UnreadCount 변경 시 UI 실시간 반영
/// </summary>
public class ChatListItem : INotifyPropertyChanged
{
    [JsonProperty("partner_id")]
    public Guid PartnerId { get; set; }

    [JsonProperty("partner_name")]
    public string PartnerName { get; set; } = "";

    [JsonProperty("partner_role")]
    public string PartnerRole { get; set; } = "";

    [JsonProperty("last_message")]
    public string LastMessage { get; set; } = "";

    [JsonProperty("last_message_at")]
    public DateTimeOffset LastMessageAt { get; set; }

    private long _unreadCount;

    [JsonProperty("unread_count")]
    public long UnreadCount
    {
        get => _unreadCount;
        set
        {
            if (_unreadCount != value)
            {
                _unreadCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasUnread));
            }
        }
    }

    // UI 헬퍼
    [JsonIgnore]
    public bool HasUnread => UnreadCount > 0;

    [JsonIgnore]
    public string PartnerRoleDisplayName => PartnerRole switch
    {
        "super_admin_mois" => "총괄관리자(행정)",
        "super_admin_army" => "총괄관리자(군)",
        "middle_local" => "중간관리자(행정)",
        "middle_military" => "중간관리자(군)",
        "user_local" => "담당자(행정)",
        "user_military" => "담당자(군)",
        "viewer_military" => "여단총괄",
        _ => "사용자"
    };

    [JsonIgnore]
    public bool IsPartnerLocal => PartnerRole is "user_local" or "middle_local" or "super_admin_mois";

    [JsonIgnore]
    public string RoleColor => IsPartnerLocal ? "#007AFF" : "#34C759";

    [JsonIgnore]
    public string RoleIcon => IsPartnerLocal ? "민" : "군";

    [JsonIgnore]
    public string RoleIconColor => "#FFFFFF";

    // Notification.TimeAgo 패턴 재사용
    [JsonIgnore]
    public string TimeAgo
    {
        get
        {
            var diff = DateTimeOffset.UtcNow - LastMessageAt;
            if (diff.TotalMinutes < 1) return "방금 전";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}분 전";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}시간 전";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}일 전";
            return LastMessageAt.LocalDateTime.ToString("MM월 dd일");
        }
    }

    // INotifyPropertyChanged 구현
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
