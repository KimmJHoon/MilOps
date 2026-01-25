using System;
using Newtonsoft.Json;
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

    [Column("name")]
    public string Name { get; set; } = "";

    [Column("phone")]
    public string Phone { get; set; } = "";

    [Column("email")]
    public string? Email { get; set; }

    [Column("region_id")]
    public Guid? RegionId { get; set; }

    [Column("district_id")]
    public Guid? DistrictId { get; set; }

    [Column("division_id")]
    public Guid? DivisionId { get; set; }

    [Column("battalion_id")]
    public Guid? BattalionId { get; set; }

    [Column("military_rank")]
    public string? MilitaryRank { get; set; }

    [Column("department")]
    public string? Department { get; set; }

    [Column("position")]
    public string? Position { get; set; }

    [Column("invited_by")]
    public Guid InvitedBy { get; set; }

    [Column("created_by")]
    public Guid? CreatedBy { get; set; }

    [Column("status")]
    public string Status { get; set; } = "pending";

    [Column("accepted_user_id")]
    public Guid? AcceptedUserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("accepted_at")]
    public DateTime? AcceptedAt { get; set; }

    // Helper properties (DB에 저장되지 않음 - JsonIgnore로 직렬화 제외)
    [JsonIgnore]
    public bool IsUsed => Status == "accepted";

    [JsonIgnore]
    public bool IsExpired => Status == "expired" || (Status == "pending" && DateTime.UtcNow > ExpiresAt);

    [JsonIgnore]
    public bool IsValid => Status == "pending" && DateTime.UtcNow <= ExpiresAt;

    [JsonIgnore]
    public string StatusText
    {
        get
        {
            if (IsUsed) return "사용됨";
            if (IsExpired) return "만료됨";
            if (Status == "cancelled") return "취소됨";

            var daysLeft = (ExpiresAt - DateTime.UtcNow).Days;
            return daysLeft > 0 ? $"초대중 (D-{daysLeft})" : "초대중 (오늘 만료)";
        }
    }

    [JsonIgnore]
    public string StatusColor
    {
        get
        {
            if (IsUsed) return "#4CAF50";    // Green
            if (IsExpired) return "#F44336"; // Red
            if (Status == "cancelled") return "#888888"; // Gray
            return "#FF9800";                // Orange
        }
    }

    [JsonIgnore]
    public string RoleDisplayName => Role switch
    {
        "middle_local" => "지자체(도) 중간관리자",
        "middle_military" => "사단담당자 중간관리자",
        "user_local" => "지자체담당자",
        "user_military" => "대대담당자",
        _ => "알 수 없음"
    };

    [JsonIgnore]
    public string CreatedAtText => $"생성일: {CreatedAt:yyyy-MM-dd}";

    [JsonIgnore]
    public string ExpiresAtText => $"만료일: {ExpiresAt:yyyy-MM-dd}";

    // UI 전용 속성 (DB에 저장되지 않음)
    [JsonIgnore]
    public string? DisplayAffiliation { get; set; }
}
