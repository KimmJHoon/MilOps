using System;
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

    [Column("phone")]
    public string? Phone { get; set; }

    [Column("email")]
    public string? Email { get; set; }

    [Column("role")]
    public string? Role { get; set; }

    [Column("region_id")]
    public Guid? RegionId { get; set; }

    [Column("district_id")]
    public Guid? DistrictId { get; set; }

    [Column("division_id")]
    public Guid? DivisionId { get; set; }

    [Column("battalion_id")]
    public Guid? BattalionId { get; set; }

    [Column("department")]
    public string? Department { get; set; }

    [Column("position")]
    public string? Position { get; set; }

    [Column("parent_id")]
    public Guid? ParentId { get; set; }

    [Column("fcm_token")]
    public string? FcmToken { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    // Helper properties
    public bool IsSuperAdmin => Role == "super_admin_mois" || Role == "super_admin_army";
    public bool IsMiddleManager => Role == "middle_local" || Role == "middle_military";
    public bool IsLocalUser => Role == "middle_local" || Role == "user_local";
    public bool IsMilitaryUser => Role == "middle_military" || Role == "user_military" || Role == "super_admin_army";

    public string RoleDisplayName => Role switch
    {
        "super_admin_mois" => "행정안전부 관리자",
        "super_admin_army" => "육군본부 관리자",
        "middle_local" => "지자체 담당자",
        "middle_military" => "군부대 담당자",
        "user_local" => "지자체 실무자",
        "user_military" => "군부대 실무자",
        _ => "알 수 없음"
    };
}
