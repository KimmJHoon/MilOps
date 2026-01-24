using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace MilOps.Models;

[Table("invitations")]
public class Invitation : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("invite_code")]
    public string InviteCode { get; set; } = "";

    [Column("role")]
    public string Role { get; set; } = "";

    [Column("region_id")]
    public Guid? RegionId { get; set; }

    [Column("district_id")]
    public Guid? DistrictId { get; set; }

    [Column("division_id")]
    public Guid? DivisionId { get; set; }

    [Column("battalion_id")]
    public Guid? BattalionId { get; set; }

    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    [Column("used_by")]
    public Guid? UsedBy { get; set; }

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("used_at")]
    public DateTime? UsedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    // Helper properties
    public bool IsUsed => UsedBy.HasValue;
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    public bool IsValid => !IsUsed && !IsExpired;

    public string Status
    {
        get
        {
            if (IsUsed) return "used";
            if (IsExpired) return "expired";
            return "pending";
        }
    }

    public string StatusText
    {
        get
        {
            if (IsUsed) return "사용됨";
            if (IsExpired) return "만료됨";

            var daysLeft = (ExpiresAt - DateTime.UtcNow).Days;
            return daysLeft > 0 ? $"초대중 (D-{daysLeft})" : "초대중 (오늘 만료)";
        }
    }

    public string StatusColor
    {
        get
        {
            if (IsUsed) return "#4CAF50";    // Green
            if (IsExpired) return "#F44336"; // Red
            return "#FF9800";                // Orange
        }
    }

    public string RoleDisplayName => Role switch
    {
        "middle_local" => "지자체 담당자",
        "middle_military" => "군부대 담당자",
        "user_local" => "지자체 실무자",
        "user_military" => "군부대 실무자",
        _ => "알 수 없음"
    };

    public string CreatedAtText => $"생성일: {CreatedAt:yyyy-MM-dd}";
    public string ExpiresAtText => $"만료일: {ExpiresAt:yyyy-MM-dd}";
}
