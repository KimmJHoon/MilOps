using System;
using Newtonsoft.Json;

namespace MilOps.Models;

/// <summary>
/// 일정 입력 상세 정보 DTO (RPC 함수 반환값)
/// </summary>
public class ScheduleInputDetailDto
{
    [JsonProperty("schedule_id")]
    public Guid ScheduleId { get; set; }

    [JsonProperty("schedule_status")]
    public string ScheduleStatus { get; set; } = "";

    [JsonProperty("available_start")]
    public DateTime? AvailableStart { get; set; }

    [JsonProperty("available_end")]
    public DateTime? AvailableEnd { get; set; }

    [JsonProperty("memo")]
    public string? Memo { get; set; }

    // 업체 정보
    [JsonProperty("company_id")]
    public Guid CompanyId { get; set; }

    [JsonProperty("company_name")]
    public string CompanyName { get; set; } = "";

    [JsonProperty("company_products")]
    public string? CompanyProducts { get; set; }

    // 지역 정보
    [JsonProperty("district_id")]
    public Guid? DistrictId { get; set; }

    [JsonProperty("district_name")]
    public string? DistrictName { get; set; }

    [JsonProperty("region_id")]
    public Guid? RegionId { get; set; }

    [JsonProperty("region_name")]
    public string? RegionName { get; set; }

    // 지자체담당자 정보
    [JsonProperty("local_user_id")]
    public Guid LocalUserId { get; set; }

    [JsonProperty("local_user_name")]
    public string LocalUserName { get; set; } = "";

    [JsonProperty("local_user_position")]
    public string? LocalUserPosition { get; set; }

    [JsonProperty("local_user_military_rank")]
    public string? LocalUserMilitaryRank { get; set; }

    // 대대담당자 정보
    [JsonProperty("military_user_id")]
    public Guid MilitaryUserId { get; set; }

    [JsonProperty("military_user_name")]
    public string MilitaryUserName { get; set; } = "";

    [JsonProperty("military_user_rank")]
    public string? MilitaryUserRank { get; set; }

    [JsonProperty("military_user_phone")]
    public string? MilitaryUserPhone { get; set; }

    // 대대/사단 정보
    [JsonProperty("battalion_id")]
    public Guid? BattalionId { get; set; }

    [JsonProperty("battalion_name")]
    public string? BattalionName { get; set; }

    [JsonProperty("division_id")]
    public Guid? DivisionId { get; set; }

    [JsonProperty("division_name")]
    public string? DivisionName { get; set; }

    // Helper property
    [JsonIgnore]
    public string LocalUserFullDisplayName
    {
        get
        {
            if (!string.IsNullOrEmpty(LocalUserMilitaryRank))
                return $"{LocalUserMilitaryRank} {LocalUserName}";
            if (!string.IsNullOrEmpty(LocalUserPosition))
                return $"{LocalUserPosition} {LocalUserName}";
            return LocalUserName;
        }
    }

    [JsonIgnore]
    public string MilitaryUserFullDisplayName
    {
        get
        {
            if (!string.IsNullOrEmpty(MilitaryUserRank))
                return $"{MilitaryUserRank} {MilitaryUserName}";
            return MilitaryUserName;
        }
    }

    [JsonIgnore]
    public string BattalionFullName
    {
        get
        {
            if (!string.IsNullOrEmpty(DivisionName) && !string.IsNullOrEmpty(BattalionName))
                return $"{DivisionName} {BattalionName}";
            return BattalionName ?? "";
        }
    }
}
