using System;
using Newtonsoft.Json;

namespace MilOps.Models;

/// <summary>
/// 캘린더 일정 조회 RPC 함수 반환값 DTO
/// get_calendar_schedules() 함수의 결과를 매핑
/// </summary>
public class CalendarScheduleDto
{
    // Schedule 기본 필드
    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("company_id")]
    public Guid CompanyId { get; set; }

    [JsonProperty("local_user_id")]
    public Guid LocalUserId { get; set; }

    [JsonProperty("military_user_id")]
    public Guid MilitaryUserId { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; } = "";

    [JsonProperty("reserved_date")]
    public DateTime? ReservedDate { get; set; }

    [JsonProperty("reserved_start_time")]
    public TimeSpan? ReservedStartTime { get; set; }

    [JsonProperty("reserved_end_time")]
    public TimeSpan? ReservedEndTime { get; set; }

    [JsonProperty("local_confirmed")]
    public bool LocalConfirmed { get; set; }

    [JsonProperty("military_confirmed")]
    public bool MilitaryConfirmed { get; set; }

    [JsonProperty("memo")]
    public string? Memo { get; set; }

    // Company 정보
    [JsonProperty("company_name")]
    public string? CompanyName { get; set; }

    // LocalUser 정보
    [JsonProperty("local_user_name")]
    public string? LocalUserName { get; set; }

    [JsonProperty("local_district_id")]
    public Guid? LocalDistrictId { get; set; }

    [JsonProperty("local_district_name")]
    public string? LocalDistrictName { get; set; }

    [JsonProperty("local_region_id")]
    public Guid? LocalRegionId { get; set; }

    [JsonProperty("local_region_name")]
    public string? LocalRegionName { get; set; }

    // MilitaryUser 정보
    [JsonProperty("military_user_name")]
    public string? MilitaryUserName { get; set; }

    [JsonProperty("military_battalion_id")]
    public Guid? MilitaryBattalionId { get; set; }

    [JsonProperty("military_battalion_name")]
    public string? MilitaryBattalionName { get; set; }

    [JsonProperty("military_division_id")]
    public Guid? MilitaryDivisionId { get; set; }

    [JsonProperty("military_division_name")]
    public string? MilitaryDivisionName { get; set; }

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
        "created" => "#9E9E9E",
        "inputted" => "#2196F3",
        "reserved" => "#FF9800",
        "confirmed" => "#4CAF50",
        _ => "#9E9E9E"
    };

    [JsonIgnore]
    public bool IsConfirmed => Status == "confirmed";

    /// <summary>
    /// DTO를 Schedule + Navigation Properties로 변환
    /// </summary>
    public Schedule ToScheduleWithNavigation()
    {
        var schedule = new Schedule
        {
            Id = Id,
            CompanyId = CompanyId,
            LocalUserId = LocalUserId,
            MilitaryUserId = MilitaryUserId,
            Status = Status,
            ReservedDate = ReservedDate,
            ReservedStartTime = ReservedStartTime,
            ReservedEndTime = ReservedEndTime,
            LocalConfirmed = LocalConfirmed,
            MilitaryConfirmed = MilitaryConfirmed,
            Memo = Memo
        };

        // Company 설정
        if (!string.IsNullOrEmpty(CompanyName))
        {
            schedule.Company = new Company
            {
                Id = CompanyId,
                Name = CompanyName
            };
        }

        // LocalUser + District + Region 설정
        if (!string.IsNullOrEmpty(LocalUserName))
        {
            schedule.LocalUser = new User
            {
                Id = LocalUserId,
                Name = LocalUserName,
                DistrictId = LocalDistrictId
            };

            if (LocalDistrictId.HasValue && !string.IsNullOrEmpty(LocalDistrictName))
            {
                schedule.LocalUser.District = new District
                {
                    Id = LocalDistrictId.Value,
                    Name = LocalDistrictName,
                    RegionId = LocalRegionId ?? Guid.Empty
                };

                if (LocalRegionId.HasValue && !string.IsNullOrEmpty(LocalRegionName))
                {
                    schedule.LocalUser.District.Region = new Region
                    {
                        Id = LocalRegionId.Value,
                        Name = LocalRegionName
                    };
                }
            }
        }

        // MilitaryUser + Battalion + Division 설정
        if (!string.IsNullOrEmpty(MilitaryUserName))
        {
            schedule.MilitaryUser = new User
            {
                Id = MilitaryUserId,
                Name = MilitaryUserName,
                BattalionId = MilitaryBattalionId
            };

            if (MilitaryBattalionId.HasValue && !string.IsNullOrEmpty(MilitaryBattalionName))
            {
                schedule.MilitaryUser.Battalion = new Battalion
                {
                    Id = MilitaryBattalionId.Value,
                    Name = MilitaryBattalionName,
                    DivisionId = MilitaryDivisionId ?? Guid.Empty
                };

                if (MilitaryDivisionId.HasValue && !string.IsNullOrEmpty(MilitaryDivisionName))
                {
                    schedule.MilitaryUser.Battalion.Division = new Division
                    {
                        Id = MilitaryDivisionId.Value,
                        Name = MilitaryDivisionName
                    };
                }
            }
        }

        return schedule;
    }
}
