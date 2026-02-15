using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MilOps.Models;
using MilOps.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace MilOps.ViewModels;

/// <summary>
/// 담당자 관리 탭 - 해당 지역의 행정/군대 담당자 목록 표시
/// </summary>
public partial class ManagerViewModel : ViewModelBase
{
    // === 통계 ===
    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _localCount;

    [ObservableProperty]
    private int _militaryCount;

    // === 필터 ===
    [ObservableProperty]
    private string _filterType = "all"; // all, local, military

    // === 유저 목록 ===
    [ObservableProperty]
    private ObservableCollection<UserListItem> _users = new();

    // === 상태 ===
    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private string _headerTitle = "담당자 목록";

    // === 내부 캐시 ===
    private List<UserListItem> _allUserItems = new();
    private Dictionary<Guid, string> _regionNames = new();
    private Dictionary<Guid, string> _districtNames = new();
    private Dictionary<Guid, string> _divisionNames = new();
    private Dictionary<Guid, string> _battalionNames = new();

    public ManagerViewModel()
    {
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadUsersAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadUsersAsync();
    }

    private async Task LoadUsersAsync()
    {
        IsLoading = true;
        ErrorMessage = "";

        try
        {
            var client = SupabaseService.Client;
            var currentUser = AuthService.CurrentUser;
            if (currentUser == null)
            {
                ErrorMessage = "로그인 정보가 없습니다.";
                return;
            }

            // 조직 데이터 병렬 로드
            var regionsTask = client.From<Region>()
                .Filter("is_active", Supabase.Postgrest.Constants.Operator.Equals, "true")
                .Get();
            var districtsTask = client.From<District>()
                .Filter("is_active", Supabase.Postgrest.Constants.Operator.Equals, "true")
                .Get();
            var divisionsTask = client.From<Division>().Get();
            var battalionsTask = client.From<Battalion>()
                .Filter("is_active", Supabase.Postgrest.Constants.Operator.Equals, "true")
                .Get();
            var usersTask = client.From<User>()
                .Filter("is_active", Supabase.Postgrest.Constants.Operator.Equals, "true")
                .Get();

            await Task.WhenAll(regionsTask, districtsTask, divisionsTask, battalionsTask, usersTask);

            var regions = regionsTask.Result.Models;
            var districts = districtsTask.Result.Models;
            var divisions = divisionsTask.Result.Models;
            var battalions = battalionsTask.Result.Models;
            var allUsers = usersTask.Result.Models;

            // 조직명 캐시 구성
            _regionNames = regions.ToDictionary(r => r.Id, r => r.Name);
            _districtNames = districts.ToDictionary(d => d.Id, d => d.Name);
            _divisionNames = divisions.ToDictionary(d => d.Id, d => d.Name);
            _battalionNames = battalions.ToDictionary(b => b.Id, b => b.Name);

            // 역할에 따른 유저 필터링
            var filteredUsers = FilterUsersByRole(allUsers, currentUser);

            // 삭제된 유저 제외
            filteredUsers = filteredUsers.Where(u => !u.IsDeleted).ToList();

            // UserListItem 변환
            _allUserItems = filteredUsers.Select(u => CreateUserListItem(u)).ToList();

            // 통계 갱신
            TotalCount = _allUserItems.Count;
            LocalCount = _allUserItems.Count(u => u.IsLocalSide);
            MilitaryCount = _allUserItems.Count(u => !u.IsLocalSide);

            // 헤더 타이틀 설정
            SetHeaderTitle(currentUser);

            // 필터 적용하여 UI 갱신
            ApplyFilter();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ManagerViewModel] LoadUsersAsync error: {ex.Message}");
            ErrorMessage = "담당자 목록을 불러오는 중 오류가 발생했습니다.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 현재 사용자 역할에 따라 볼 수 있는 유저 필터링
    /// </summary>
    private List<User> FilterUsersByRole(List<User> allUsers, User currentUser)
    {
        return currentUser.Role switch
        {
            // 최종관리자: 전체 유저
            "super_admin_mois" or "super_admin_army" => allUsers,

            // 지자체(도): 같은 region의 유저
            "middle_local" => allUsers
                .Where(u => u.RegionId == currentUser.RegionId)
                .ToList(),

            // 사단담당자: 같은 division의 유저 + 관련 지자체 유저
            "middle_military" => FilterForDivisionManager(allUsers, currentUser),

            // 지자체담당자: 같은 district의 유저 + 관련 대대 유저
            "user_local" => allUsers
                .Where(u => u.DistrictId == currentUser.DistrictId ||
                            u.RegionId == currentUser.RegionId)
                .ToList(),

            // 대대담당자: 같은 battalion + 관련 지자체 유저
            "user_military" => allUsers
                .Where(u => u.BattalionId == currentUser.BattalionId ||
                            u.DivisionId == currentUser.DivisionId)
                .ToList(),

            _ => new List<User>()
        };
    }

    /// <summary>
    /// 사단담당자용 필터 - 같은 사단 + 관련 지역 유저
    /// </summary>
    private List<User> FilterForDivisionManager(List<User> allUsers, User currentUser)
    {
        // 같은 사단의 군 유저
        var militaryUsers = allUsers
            .Where(u => u.DivisionId == currentUser.DivisionId)
            .ToList();

        // 관련 지역 유저도 포함 (같은 region)
        var localUsers = allUsers
            .Where(u => u.IsLocalSide && u.RegionId == currentUser.RegionId)
            .ToList();

        return militaryUsers.Union(localUsers).ToList();
    }

    private UserListItem CreateUserListItem(User user)
    {
        string affiliation = BuildAffiliation(user);

        return new UserListItem
        {
            Id = user.Id,
            Name = user.FullDisplayName,
            LoginId = user.LoginId,
            RoleDisplayName = user.RoleDisplayName,
            Affiliation = affiliation,
            IsLocalSide = user.IsLocalSide,
            Role = user.Role
        };
    }

    /// <summary>
    /// 소속 표시 문자열 생성
    /// </summary>
    private string BuildAffiliation(User user)
    {
        if (user.IsLocalSide)
        {
            // 행정 측: "강원도 춘천시" 형태
            var parts = new List<string>();
            if (user.RegionId.HasValue && _regionNames.TryGetValue(user.RegionId.Value, out var regionName))
                parts.Add(regionName);
            if (user.DistrictId.HasValue && _districtNames.TryGetValue(user.DistrictId.Value, out var districtName))
                parts.Add(districtName);
            return string.Join(" ", parts);
        }
        else
        {
            // 군 측: "31사단 작전대대" 형태
            var parts = new List<string>();
            if (user.DivisionId.HasValue && _divisionNames.TryGetValue(user.DivisionId.Value, out var divisionName))
                parts.Add(divisionName);
            if (user.BattalionId.HasValue && _battalionNames.TryGetValue(user.BattalionId.Value, out var battalionName))
                parts.Add(battalionName);
            return string.Join(" ", parts);
        }
    }

    private void SetHeaderTitle(User currentUser)
    {
        HeaderTitle = currentUser.Role switch
        {
            "super_admin_mois" or "super_admin_army" => "전체 담당자 목록",
            "middle_local" => _regionNames.TryGetValue(currentUser.RegionId ?? Guid.Empty, out var rn)
                ? $"{rn} 담당자 목록" : "담당자 목록",
            "middle_military" => _divisionNames.TryGetValue(currentUser.DivisionId ?? Guid.Empty, out var dn)
                ? $"{dn} 담당자 목록" : "담당자 목록",
            _ => "담당자 목록"
        };
    }

    // === 필터 커맨드 ===

    [RelayCommand]
    private void SetFilterAll()
    {
        FilterType = "all";
        ApplyFilter();
    }

    [RelayCommand]
    private void SetFilterLocal()
    {
        FilterType = "local";
        ApplyFilter();
    }

    [RelayCommand]
    private void SetFilterMilitary()
    {
        FilterType = "military";
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filtered = FilterType switch
        {
            "local" => _allUserItems.Where(u => u.IsLocalSide).ToList(),
            "military" => _allUserItems.Where(u => !u.IsLocalSide).ToList(),
            _ => _allUserItems
        };

        Users.Clear();
        foreach (var user in filtered.OrderBy(u => u.RoleDisplayName).ThenBy(u => u.Name))
        {
            Users.Add(user);
        }
    }

    /// <summary>
    /// ViewModel 정리
    /// </summary>
    public void Cleanup()
    {
        // 리소스 정리
    }

    /// <summary>
    /// 캐시 및 데이터 비우기 (로그아웃 시 호출)
    /// </summary>
    public void ClearCache()
    {
        _allUserItems.Clear();
        _regionNames.Clear();
        _districtNames.Clear();
        _divisionNames.Clear();
        _battalionNames.Clear();
        Users.Clear();
        TotalCount = 0;
        LocalCount = 0;
        MilitaryCount = 0;
    }
}

/// <summary>
/// 유저 목록 표시용 아이템
/// </summary>
public class UserListItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string LoginId { get; set; } = "";
    public string RoleDisplayName { get; set; } = "";
    public string Affiliation { get; set; } = "";
    public bool IsLocalSide { get; set; }
    public string Role { get; set; } = "";

    /// <summary>
    /// 역할 아이콘 색상 (행정=파랑, 군=초록)
    /// </summary>
    public string RoleColor => IsLocalSide ? "#007AFF" : "#34C759";

    /// <summary>
    /// 역할 아이콘 텍스트
    /// </summary>
    public string RoleIcon => IsLocalSide ? "민" : "군";
}
