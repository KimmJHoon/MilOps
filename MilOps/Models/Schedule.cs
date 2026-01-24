using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace MilOps.Models;

[Table("schedules")]
public class Schedule : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("title")]
    public string Title { get; set; } = "";

    [Column("description")]
    public string? Description { get; set; }

    [Column("status")]
    public string Status { get; set; } = "created";

    [Column("creator_id")]
    public Guid CreatorId { get; set; }

    [Column("target_battalion_id")]
    public Guid? TargetBattalionId { get; set; }

    [Column("local_manager_id")]
    public Guid? LocalManagerId { get; set; }

    [Column("military_manager_id")]
    public Guid? MilitaryManagerId { get; set; }

    [Column("confirmed_time_id")]
    public Guid? ConfirmedTimeId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    // Helper properties
    public string StatusDisplayName => Status switch
    {
        "created" => "생성됨",
        "inputted" => "시간 입력됨",
        "reserved" => "예약됨",
        "confirmed" => "확정됨",
        _ => "알 수 없음"
    };

    public string StatusColor => Status switch
    {
        "created" => "#9E9E9E",      // Gray
        "inputted" => "#2196F3",     // Blue
        "reserved" => "#FF9800",     // Orange
        "confirmed" => "#4CAF50",    // Green
        _ => "#9E9E9E"
    };

    public bool CanInputTime => Status == "created";
    public bool CanReserve => Status == "inputted";
    public bool CanConfirm => Status == "reserved";
    public bool IsCompleted => Status == "confirmed";
}

[Table("schedule_available_times")]
public class ScheduleAvailableTime : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("schedule_id")]
    public Guid ScheduleId { get; set; }

    [Column("available_date")]
    public DateTime AvailableDate { get; set; }

    [Column("start_time")]
    public TimeSpan StartTime { get; set; }

    [Column("end_time")]
    public TimeSpan EndTime { get; set; }

    [Column("is_selected")]
    public bool IsSelected { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    // Helper properties
    public string TimeRangeDisplay => $"{StartTime:hh\\:mm} - {EndTime:hh\\:mm}";
    public string DateDisplay => AvailableDate.ToString("yyyy-MM-dd (ddd)");
    public string FullDisplay => $"{DateDisplay} {TimeRangeDisplay}";
}
