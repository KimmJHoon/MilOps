using System;
using Newtonsoft.Json;

namespace MilOps.Models;

/// <summary>
/// 일정 확정 상세 정보 DTO (RPC 함수 반환값)
/// </summary>
public class ScheduleConfirmDetailDto
{
    [JsonProperty("schedule_id")]
    public Guid ScheduleId { get; set; }

    [JsonProperty("schedule_status")]
    public string ScheduleStatus { get; set; } = "";

    [JsonProperty("schedule_status_order")]
    public int ScheduleStatusOrder { get; set; }

    [JsonProperty("reserved_date")]
    public DateTime? ReservedDate { get; set; }

    [JsonProperty("reserved_start_time")]
    public TimeSpan? ReservedStartTime { get; set; }

    [JsonProperty("reserved_end_time")]
    public TimeSpan? ReservedEndTime { get; set; }

    [JsonProperty("local_confirmed")]
    public bool LocalConfirmed { get; set; }

    [JsonProperty("local_confirmed_at")]
    public DateTime? LocalConfirmedAt { get; set; }

    [JsonProperty("military_confirmed")]
    public bool MilitaryConfirmed { get; set; }

    [JsonProperty("military_confirmed_at")]
    public DateTime? MilitaryConfirmedAt { get; set; }

    [JsonProperty("memo")]
    public string? Memo { get; set; }

    // 업체 정보
    [JsonProperty("company_id")]
    public Guid CompanyId { get; set; }

    [JsonProperty("company_name")]
    public string CompanyName { get; set; } = "";

    [JsonProperty("company_address")]
    public string? CompanyAddress { get; set; }

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

    [JsonProperty("local_user_phone")]
    public string? LocalUserPhone { get; set; }

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
}
