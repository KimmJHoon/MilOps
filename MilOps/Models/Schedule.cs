using System;
using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace MilOps.Models;

[Table("schedules")]
public class Schedule : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("company_id")]
    public Guid CompanyId { get; set; }

    [Column("local_user_id")]
    public Guid LocalUserId { get; set; }

    [Column("military_user_id")]
    public Guid MilitaryUserId { get; set; }

    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    [Column("status")]
    public string Status { get; set; } = "created";

    [Column("status_order")]
    public int StatusOrder { get; set; } = 1;

    [Column("available_start")]
    public DateTime? AvailableStart { get; set; }

    [Column("available_end")]
    public DateTime? AvailableEnd { get; set; }

    [Column("reserved_date")]
    public DateTime? ReservedDate { get; set; }

    [Column("reserved_start_time")]
    public TimeSpan? ReservedStartTime { get; set; }

    [Column("reserved_end_time")]
    public TimeSpan? ReservedEndTime { get; set; }

    [Column("local_confirmed")]
    public bool LocalConfirmed { get; set; }

    [Column("local_confirmed_at")]
    public DateTime? LocalConfirmedAt { get; set; }

    [Column("military_confirmed")]
    public bool MilitaryConfirmed { get; set; }

    [Column("military_confirmed_at")]
    public DateTime? MilitaryConfirmedAt { get; set; }

    [Column("memo")]
    public string? Memo { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [Column("deleted_by")]
    public Guid? DeletedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [Column("confirmed_at")]
    public DateTime? ConfirmedAt { get; set; }

    // Navigation properties (DB에 저장되지 않음 - JsonIgnore로 직렬화 제외)
    [JsonIgnore]
    public Company? Company { get; set; }
    [JsonIgnore]
    public User? LocalUser { get; set; }
    [JsonIgnore]
    public User? MilitaryUser { get; set; }
    [JsonIgnore]
    public User? Creator { get; set; }

    // Helper properties
    [JsonIgnore]
    public string StatusDisplayName => Status switch
    {
        "created" => "생성됨",
        "inputted" => "입력됨",
        "reserved" => "예약됨",
        "confirmed" => "확정됨",
        _ => "알 수 없음"
    };

    [JsonIgnore]
    public string StatusColor => Status switch
    {
        "created" => "#9E9E9E",      // Gray
        "inputted" => "#2196F3",     // Blue
        "reserved" => "#FF9800",     // Orange
        "confirmed" => "#4CAF50",    // Green
        _ => "#9E9E9E"
    };

    [JsonIgnore]
    public bool CanInput => Status == "created";
    [JsonIgnore]
    public bool CanReserve => Status == "inputted";
    [JsonIgnore]
    public bool CanConfirm => Status == "reserved";
    [JsonIgnore]
    public bool IsConfirmed => Status == "confirmed";
    [JsonIgnore]
    public bool IsDeleted => DeletedAt.HasValue;

    [JsonIgnore]
    public bool BothConfirmed => LocalConfirmed && MilitaryConfirmed;

    [JsonIgnore]
    public string ReservedTimeDisplay
    {
        get
        {
            if (!ReservedDate.HasValue) return "";
            var date = ReservedDate.Value.ToString("yyyy-MM-dd");
            if (ReservedStartTime.HasValue && ReservedEndTime.HasValue)
            {
                return $"{date} {ReservedStartTime.Value:hh\\:mm}-{ReservedEndTime.Value:hh\\:mm}";
            }
            return date;
        }
    }

    [JsonIgnore]
    public string AvailableDateRangeDisplay
    {
        get
        {
            if (!AvailableStart.HasValue || !AvailableEnd.HasValue) return "";
            return $"{AvailableStart.Value:yyyy-MM-dd} ~ {AvailableEnd.Value:yyyy-MM-dd}";
        }
    }
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
    [JsonIgnore]
    public string TimeRangeDisplay => $"{StartTime:hh\\:mm}-{EndTime:hh\\:mm}";
    [JsonIgnore]
    public string DateDisplay => AvailableDate.ToString("yyyy-MM-dd");
    [JsonIgnore]
    public string FullDisplay => $"{DateDisplay} {TimeRangeDisplay}";
}
