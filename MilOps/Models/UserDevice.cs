using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace MilOps.Models;

/// <summary>
/// 사용자 기기 정보 (FCM 토큰 관리)
/// </summary>
[Table("user_devices")]
public class UserDevice : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("fcm_token")]
    public string FcmToken { get; set; } = string.Empty;

    [Column("device_type")]
    public string DeviceType { get; set; } = "android";

    [Column("device_name")]
    public string? DeviceName { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("last_used_at")]
    public DateTime? LastUsedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
