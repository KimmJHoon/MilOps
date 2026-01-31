using System;
using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace MilOps.Models;

[Table("companies")]
public class Company : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = "";

    [Column("address")]
    public string Address { get; set; } = "";

    [Column("products")]
    public string? Products { get; set; }

    [Column("contact_name")]
    public string? ContactName { get; set; }

    [Column("contact_phone")]
    public string? ContactPhone { get; set; }

    [Column("district_id")]
    public Guid DistrictId { get; set; }

    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("deleted_at")]
    public DateTime? DeletedAt { get; set; }

    [Column("deleted_by")]
    public Guid? DeletedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    // Navigation properties (DB에 저장되지 않음 - JsonIgnore로 직렬화 제외)
    [JsonIgnore]
    public District? District { get; set; }

    // Helper properties
    [JsonIgnore]
    public string DisplayName => Name;
    [JsonIgnore]
    public bool IsDeleted => DeletedAt.HasValue;
}
