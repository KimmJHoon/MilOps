using System;
using MilOps.ViewModels;
using Newtonsoft.Json;

namespace MilOps.Models;

/// <summary>
/// 담당자 관리 RPC 함수 반환값 DTO
/// get_manager_users() 함수의 결과를 매핑
/// </summary>
public class ManagerUserDto
{
    [JsonProperty("user_id")]
    public Guid UserId { get; set; }

    [JsonProperty("login_id")]
    public string LoginId { get; set; } = "";

    [JsonProperty("user_name")]
    public string UserName { get; set; } = "";

    [JsonProperty("user_role")]
    public string UserRole { get; set; } = "";

    [JsonProperty("military_rank")]
    public string? MilitaryRank { get; set; }

    [JsonProperty("is_local_side")]
    public bool IsLocalSide { get; set; }

    // 소속 정보 (서버에서 JOIN 완료)
    [JsonProperty("region_id")]
    public Guid? RegionId { get; set; }

    [JsonProperty("region_name")]
    public string? RegionName { get; set; }

    [JsonProperty("district_id")]
    public Guid? DistrictId { get; set; }

    [JsonProperty("district_name")]
    public string? DistrictName { get; set; }

    [JsonProperty("division_id")]
    public Guid? DivisionId { get; set; }

    [JsonProperty("division_name")]
    public string? DivisionName { get; set; }

    [JsonProperty("brigade_id")]
    public Guid? BrigadeId { get; set; }

    [JsonProperty("brigade_name")]
    public string? BrigadeName { get; set; }

    [JsonProperty("battalion_id")]
    public Guid? BattalionId { get; set; }

    [JsonProperty("battalion_name")]
    public string? BattalionName { get; set; }

    // === Helper Properties ===

    [JsonIgnore]
    public string RoleDisplayName => UserRole switch
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

    [JsonIgnore]
    public bool IsMilitarySide => !IsLocalSide;

    /// <summary>
    /// 표시명: 군 측은 "이름 계급", 행정 측은 "이름"
    /// </summary>
    [JsonIgnore]
    public string FullDisplayName
    {
        get
        {
            if (IsMilitarySide && !string.IsNullOrEmpty(MilitaryRank))
                return $"{UserName} {MilitaryRank}";
            return UserName;
        }
    }

    /// <summary>
    /// 소속 표시 문자열 (서버에서 JOIN된 조직명 사용)
    /// </summary>
    [JsonIgnore]
    public string Affiliation
    {
        get
        {
            if (IsLocalSide)
            {
                // 행정 측: "강원도 춘천시" 형태
                var parts = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrEmpty(RegionName)) parts.Add(RegionName);
                if (!string.IsNullOrEmpty(DistrictName)) parts.Add(DistrictName);
                return string.Join(" ", parts);
            }
            else
            {
                // 군 측: "31사단 93여단 작전대대" 형태
                var parts = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrEmpty(DivisionName)) parts.Add(DivisionName);
                if (!string.IsNullOrEmpty(BrigadeName)) parts.Add(BrigadeName);
                if (!string.IsNullOrEmpty(BattalionName)) parts.Add(BattalionName);
                return string.Join(" ", parts);
            }
        }
    }

    /// <summary>
    /// DTO → UserListItem 변환
    /// </summary>
    public UserListItem ToUserListItem()
    {
        return new UserListItem
        {
            Id = UserId,
            Name = FullDisplayName,
            LoginId = LoginId,
            RoleDisplayName = RoleDisplayName,
            Affiliation = Affiliation,
            IsLocalSide = IsLocalSide,
            Role = UserRole
        };
    }
}
