using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace MilOps.Models;

/// <summary>
/// 광역시/도
/// </summary>
[Table("regions")]
public class Region : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = "";

    [Column("code")]
    public string? Code { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 시/군/구 (지자체)
/// </summary>
[Table("districts")]
public class District : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("region_id")]
    public Guid RegionId { get; set; }

    [Column("name")]
    public string Name { get; set; } = "";

    [Column("code")]
    public string? Code { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 사단
/// </summary>
[Table("divisions")]
public class Division : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = "";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 대대 (군부대)
/// </summary>
[Table("battalions")]
public class Battalion : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("division_id")]
    public Guid DivisionId { get; set; }

    [Column("name")]
    public string Name { get; set; } = "";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 지자체-대대 매핑 (담당 구역)
/// </summary>
[Table("district_battalion_mapping")]
public class DistrictBattalionMapping : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("district_id")]
    public Guid DistrictId { get; set; }

    [Column("battalion_id")]
    public Guid BattalionId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
