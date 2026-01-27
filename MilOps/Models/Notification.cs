using System;
using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace MilOps.Models;

/// <summary>
/// 알림 모델
/// </summary>
[Table("notifications")]
public class Notification : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("schedule_id")]
    public Guid? ScheduleId { get; set; }

    [Column("type")]
    public string Type { get; set; } = "";

    [Column("title")]
    public string Title { get; set; } = "";

    [Column("body")]
    public string Message { get; set; } = "";

    [Column("is_read")]
    public bool IsRead { get; set; } = false;

    [Column("read_at")]
    public DateTime? ReadAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    [JsonIgnore]
    public Schedule? Schedule { get; set; }

    // Helper properties
    [JsonIgnore]
    public string TypeDisplayName => Type switch
    {
        "schedule_created" => "일정 생성",
        "schedule_inputted" => "일정 입력",
        "schedule_reserved" => "일정 예약",
        "schedule_confirmed" => "일정 확정",
        "schedule_reminder" => "일정 알림",
        _ => "알림"
    };

    [JsonIgnore]
    public string TypeIcon => Type switch
    {
        "schedule_created" => "📋",
        "schedule_inputted" => "📝",
        "schedule_reserved" => "📅",
        "schedule_confirmed" => "✅",
        "schedule_reminder" => "⏰",
        _ => "🔔"
    };

    [JsonIgnore]
    public string TypeColor => Type switch
    {
        "schedule_created" => "#9E9E9E",
        "schedule_inputted" => "#2196F3",
        "schedule_reserved" => "#FF9800",
        "schedule_confirmed" => "#4CAF50",
        "schedule_reminder" => "#F44336",
        _ => "#9E9E9E"
    };

    [JsonIgnore]
    public string TimeAgo
    {
        get
        {
            var diff = DateTime.UtcNow - CreatedAt;
            if (diff.TotalMinutes < 1) return "방금 전";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}분 전";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}시간 전";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}일 전";
            return CreatedAt.ToString("MM월 dd일");
        }
    }

    [JsonIgnore]
    public string BackgroundColor => IsRead ? "#1A1A1A" : "#2A2A3A";
}
