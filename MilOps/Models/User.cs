using System;
using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace MilOps.Models;

[Table("users")]
public class User : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("login_id")]
    public string LoginId { get; set; } = "";

    [Column("name")]
    public string Name { get; set; } = "";

    [Column("email")]
    public string? Email { get; set; }

    [Column("role")]
    public string Role { get; set; } = "";

    [Column("region_id")]
    public Guid? RegionId { get; set; }

    [Column("district_id")]
    public Guid? DistrictId { get; set; }

    [Column("division_id")]
    public Guid? DivisionId { get; set; }

    [Column("brigade_id")]
    public Guid? BrigadeId { get; set; }

    [Column("battalion_id")]
    public Guid? BattalionId { get; set; }

    [Column("military_rank")]
    public string? MilitaryRank { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    // Navigation properties (DB에 저장되지 않음 - JsonIgnore로 직렬화 제외)
    [JsonIgnore]
    public Region? Region { get; set; }
    [JsonIgnore]
    public District? District { get; set; }
    [JsonIgnore]
    public Division? Division { get; set; }
    [JsonIgnore]
    public Brigade? Brigade { get; set; }
    [JsonIgnore]
    public Battalion? Battalion { get; set; }

    // Helper properties
    [JsonIgnore]
    public bool IsSuperAdmin => Role == "super_admin_mois" || Role == "super_admin_army";
    [JsonIgnore]
    public bool IsMiddleManager => Role == "middle_local" || Role == "middle_military";
    [JsonIgnore]
    public bool IsUser => Role == "user_local" || Role == "user_military";

    [JsonIgnore]
    public bool IsLocalSide => Role == "super_admin_mois" || Role == "middle_local" || Role == "user_local";
    [JsonIgnore]
    public bool IsMilitarySide => Role == "super_admin_army" || Role == "middle_military" || Role == "viewer_military" || Role == "user_military";

    [JsonIgnore]
    public bool IsViewer => Role == "viewer_military";

    [JsonIgnore]
    public bool IsDeleted => DeletedAt.HasValue;

    [JsonIgnore]
    public string RoleDisplayName => Role switch
    {
        "super_admin_mois" => "최종관리자 (행정안전부)",
        "super_admin_army" => "최종관리자 (제2작전사)",
        "middle_local" => "지자체(도)",
        "middle_military" => "사단담당자",
        "viewer_military" => "여단총괄",
        "user_local" => "지자체담당자",
        "user_military" => "대대담당자",
        _ => "알 수 없음"
    };

    /// <summary>
    /// 군 측: 계급 표시, 행정 측: 빈 문자열
    /// </summary>
    [JsonIgnore]
    public string PositionDisplay
    {
        get
        {
            if (IsMilitarySide && !string.IsNullOrEmpty(MilitaryRank))
                return MilitaryRank;
            return "";
        }
    }

    /// <summary>
    /// 계정 표시명 (하위 호환용)
    /// 군 측: "계정명 계급", 행정 측: "계정명"
    /// </summary>
    [JsonIgnore]
    public string FullDisplayName
    {
        get
        {
            var pos = PositionDisplay;
            return string.IsNullOrEmpty(pos) ? Name : $"{Name} {pos}";
        }
    }
}
