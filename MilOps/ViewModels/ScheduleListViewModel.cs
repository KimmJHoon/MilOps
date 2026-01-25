using Avalonia.Threading;
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

public partial class ScheduleListViewModel : ViewModelBase
{
    // 일정 목록
    [ObservableProperty]
    private ObservableCollection<ScheduleListItem> _schedules = new();

    // 로딩 상태
    [ObservableProperty]
    private bool _isLoading = false;

    // 현재 사용자 역할
    [ObservableProperty]
    private string _currentUserRole = "";

    // 현재 사용자 정보 표시
    [ObservableProperty]
    private string _currentUserDisplay = "";

    // 역할 탭 표시 여부
    [ObservableProperty]
    private bool _showLocalUserTab = false;

    [ObservableProperty]
    private bool _showMilitaryUserTab = false;

    [ObservableProperty]
    private bool _showDivisionTab = false;

    [ObservableProperty]
    private bool _showRegionTab = false;

    // 선택된 역할 탭
    [ObservableProperty]
    private string _selectedRoleTab = "";

    // 상태 필터
    [ObservableProperty]
    private string _selectedStatusFilter = "all";

    // 상태별 카운트
    [ObservableProperty]
    private int _allCount = 0;

    [ObservableProperty]
    private int _createdCount = 0;

    [ObservableProperty]
    private int _inputtedCount = 0;

    [ObservableProperty]
    private int _reservedCount = 0;

    [ObservableProperty]
    private int _confirmedCount = 0;

    // 버튼 표시 여부
    [ObservableProperty]
    private bool _showCompanyRegisterButton = false;

    [ObservableProperty]
    private bool _showScheduleCreateButton = false;

    // 안내 메시지
    [ObservableProperty]
    private string _guideMessage = "";

    [ObservableProperty]
    private bool _showGuideMessage = false;

    // 빈 목록 메시지
    [ObservableProperty]
    private string _emptyMessage = "일정이 없습니다";

    [ObservableProperty]
    private bool _showEmptyMessage = false;

    // 캐시된 조직 데이터
    private Dictionary<Guid, string> _companyNames = new();
    private Dictionary<Guid, string> _battalionNames = new();
    private Dictionary<Guid, string> _districtNames = new();
    private Dictionary<Guid, string> _userNames = new();
    private Dictionary<Guid, User> _userCache = new();
    private Dictionary<Guid, Battalion> _battalionCache = new();
    private Dictionary<Guid, District> _districtCache = new();

    // 원본 일정 데이터 (필터링 전)
    private List<Schedule> _allSchedules = new();

    // 이벤트: 일정 상세 화면으로 이동
    public event Action<Schedule, string>? NavigateToScheduleDetail;

    // 이벤트: 업체 등록 화면으로 이동
    public event Action? NavigateToCompanyRegister;

    // 이벤트: 일정 생성 화면으로 이동
    public event Action? NavigateToScheduleCreate;

    public ScheduleListViewModel()
    {
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        DetermineUserRole();
        await LoadCacheDataAsync();
        await LoadSchedulesAsync();
    }

    /// <summary>
    /// 현재 사용자 역할 판단 및 UI 설정
    /// </summary>
    private void DetermineUserRole()
    {
        var currentUser = AuthService.CurrentUser;
        if (currentUser == null) return;

        CurrentUserRole = currentUser.Role;

        // 역할에 따른 탭 표시 설정
        switch (currentUser.Role)
        {
            case "user_local": // 지자체담당자
                ShowLocalUserTab = true;
                SelectedRoleTab = "user_local";
                GuideMessage = "💡 생성됨 상태의 일정을 클릭하여 가능 일정을 입력해주세요";
                ShowGuideMessage = true;
                break;

            case "user_military": // 대대담당자
                ShowMilitaryUserTab = true;
                SelectedRoleTab = "user_military";
                GuideMessage = "💡 입력됨 상태의 일정을 클릭하여 예약해주세요";
                ShowGuideMessage = true;
                break;

            case "middle_military": // 사단담당자
                ShowDivisionTab = true;
                ShowScheduleCreateButton = true;
                SelectedRoleTab = "middle_military";
                GuideMessage = "";
                ShowGuideMessage = false;
                break;

            case "middle_local": // 지자체(도)
                ShowRegionTab = true;
                ShowCompanyRegisterButton = true;
                SelectedRoleTab = "middle_local";
                GuideMessage = "";
                ShowGuideMessage = false;
                break;

            default:
                SelectedRoleTab = "";
                break;
        }

        // 사용자 정보 표시 설정
        UpdateCurrentUserDisplay(currentUser);
    }

    private void UpdateCurrentUserDisplay(User user)
    {
        var parts = new List<string>();

        // 이름 + 직급/계급
        var nameWithPosition = user.FullDisplayName;
        parts.Add(nameWithPosition);

        // 소속 정보
        var affiliation = GetUserAffiliation(user);
        if (!string.IsNullOrEmpty(affiliation))
        {
            parts.Add($"({affiliation})");
        }

        // 역할 표시
        parts.Add(user.RoleDisplayName);

        CurrentUserDisplay = string.Join(" ", parts);
    }

    private string GetUserAffiliation(User user)
    {
        // 캐시에서 조회
        if (user.DistrictId.HasValue && _districtNames.TryGetValue(user.DistrictId.Value, out var districtName))
            return districtName;

        if (user.BattalionId.HasValue && _battalionNames.TryGetValue(user.BattalionId.Value, out var battalionName))
            return battalionName;

        return "";
    }

    /// <summary>
    /// 캐시 데이터 로드 (업체, 대대, 지자체, 사용자)
    /// </summary>
    private async Task LoadCacheDataAsync()
    {
        if (!SupabaseService.IsInitialized) return;

        try
        {
            var companiesTask = SupabaseService.Client.From<Company>().Filter("is_active", Supabase.Postgrest.Constants.Operator.Equals, "true").Get();
            var battalionsTask = SupabaseService.Client.From<Battalion>().Get();
            var districtsTask = SupabaseService.Client.From<District>().Get();
            var usersTask = SupabaseService.Client.From<User>().Filter("is_active", Supabase.Postgrest.Constants.Operator.Equals, "true").Get();

            await Task.WhenAll(companiesTask, battalionsTask, districtsTask, usersTask);

            _companyNames = companiesTask.Result.Models.ToDictionary(c => c.Id, c => c.Name);
            _battalionNames = battalionsTask.Result.Models.ToDictionary(b => b.Id, b => b.Name);
            _battalionCache = battalionsTask.Result.Models.ToDictionary(b => b.Id, b => b);
            _districtNames = districtsTask.Result.Models.ToDictionary(d => d.Id, d => d.Name);
            _districtCache = districtsTask.Result.Models.ToDictionary(d => d.Id, d => d);
            _userNames = usersTask.Result.Models.ToDictionary(u => u.Id, u => u.FullDisplayName);
            _userCache = usersTask.Result.Models.ToDictionary(u => u.Id, u => u);

            // 현재 사용자 표시 갱신
            if (AuthService.CurrentUser != null)
            {
                UpdateCurrentUserDisplay(AuthService.CurrentUser);
            }

            System.Diagnostics.Debug.WriteLine($"[ScheduleListVM] Cache loaded: {_companyNames.Count} companies, {_battalionNames.Count} battalions, {_districtNames.Count} districts, {_userNames.Count} users");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScheduleListVM] Failed to load cache: {ex.Message}");
        }
    }

    /// <summary>
    /// 일정 목록 로드
    /// </summary>
    [RelayCommand]
    public async Task LoadSchedulesAsync()
    {
        if (!SupabaseService.IsInitialized) return;
        if (AuthService.CurrentUser == null) return;

        IsLoading = true;
        try
        {
            var currentUser = AuthService.CurrentUser;

            // 모든 일정을 가져온 후 클라이언트에서 필터링
            var response = await SupabaseService.Client.From<Schedule>()
                .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                .Get();

            // 삭제되지 않은 일정만 필터링
            _allSchedules = response.Models.Where(s => !s.IsDeleted).ToList();

            // 역할에 따른 추가 필터링
            _allSchedules = FilterSchedulesByRole(_allSchedules, currentUser);

            // 상태별 카운트 업데이트
            UpdateStatusCounts();

            // 필터 적용 및 UI 표시
            ApplyFilter();

            System.Diagnostics.Debug.WriteLine($"[ScheduleListVM] Loaded {_allSchedules.Count} schedules");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScheduleListVM] Failed to load schedules: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private List<Schedule> FilterSchedulesByRole(List<Schedule> schedules, User currentUser)
    {
        switch (currentUser.Role)
        {
            case "user_local":
                return schedules.Where(s => s.LocalUserId == currentUser.Id).ToList();

            case "user_military":
                return schedules.Where(s => s.MilitaryUserId == currentUser.Id && s.Status != "created").ToList();

            case "middle_military":
                // 사단담당자: 자신이 생성한 일정 또는 자신의 사단 소속 대대담당자가 배정된 일정
                if (!currentUser.DivisionId.HasValue)
                {
                    return schedules.Where(s => s.CreatedBy == currentUser.Id).ToList();
                }

                return schedules.Where(s =>
                    s.CreatedBy == currentUser.Id ||
                    IsMilitaryUserInMyDivision(s.MilitaryUserId, currentUser.DivisionId.Value)
                ).ToList();

            case "middle_local":
                // 지자체(도) 담당자: 예약됨/확정됨 상태 또는 자신의 Region 소속 LocalUser의 일정
                if (!currentUser.RegionId.HasValue)
                {
                    return schedules.Where(s => s.Status == "reserved" || s.Status == "confirmed").ToList();
                }

                return schedules.Where(s =>
                    (s.Status == "reserved" || s.Status == "confirmed") &&
                    IsLocalUserInMyRegion(s.LocalUserId, currentUser.RegionId.Value)
                ).ToList();

            default:
                return schedules;
        }
    }

    /// <summary>
    /// 대대담당자가 특정 사단 소속인지 확인
    /// </summary>
    private bool IsMilitaryUserInMyDivision(Guid militaryUserId, Guid divisionId)
    {
        if (!_userCache.TryGetValue(militaryUserId, out var militaryUser))
            return false;

        if (!militaryUser.BattalionId.HasValue)
            return false;

        if (!_battalionCache.TryGetValue(militaryUser.BattalionId.Value, out var battalion))
            return false;

        return battalion.DivisionId == divisionId;
    }

    /// <summary>
    /// 지자체담당자가 특정 Region 소속인지 확인
    /// </summary>
    private bool IsLocalUserInMyRegion(Guid localUserId, Guid regionId)
    {
        if (!_userCache.TryGetValue(localUserId, out var localUser))
            return false;

        if (!localUser.DistrictId.HasValue)
            return false;

        if (!_districtCache.TryGetValue(localUser.DistrictId.Value, out var district))
            return false;

        return district.RegionId == regionId;
    }

    private void UpdateStatusCounts()
    {
        AllCount = _allSchedules.Count;
        CreatedCount = _allSchedules.Count(s => s.Status == "created");
        InputtedCount = _allSchedules.Count(s => s.Status == "inputted");
        ReservedCount = _allSchedules.Count(s => s.Status == "reserved");
        ConfirmedCount = _allSchedules.Count(s => s.Status == "confirmed");
    }

    /// <summary>
    /// 상태 필터 적용
    /// </summary>
    private void ApplyFilter()
    {
        var filtered = SelectedStatusFilter switch
        {
            "created" => _allSchedules.Where(s => s.Status == "created"),
            "inputted" => _allSchedules.Where(s => s.Status == "inputted"),
            "reserved" => _allSchedules.Where(s => s.Status == "reserved"),
            "confirmed" => _allSchedules.Where(s => s.Status == "confirmed"),
            _ => _allSchedules
        };

        Schedules.Clear();
        foreach (var schedule in filtered)
        {
            var item = CreateScheduleListItem(schedule);
            Schedules.Add(item);
        }

        ShowEmptyMessage = Schedules.Count == 0;
        UpdateEmptyMessage();
    }

    private void UpdateEmptyMessage()
    {
        EmptyMessage = SelectedStatusFilter switch
        {
            "created" => "생성된 일정이 없습니다",
            "inputted" => "입력된 일정이 없습니다",
            "reserved" => "예약된 일정이 없습니다",
            "confirmed" => "확정된 일정이 없습니다",
            _ => "일정이 없습니다"
        };
    }

    private ScheduleListItem CreateScheduleListItem(Schedule schedule)
    {
        var item = new ScheduleListItem
        {
            Schedule = schedule,
            CompanyName = _companyNames.GetValueOrDefault(schedule.CompanyId, "알 수 없는 업체"),
            BattalionName = GetBattalionNameFromUser(schedule.MilitaryUserId),
            DistrictName = GetDistrictNameFromUser(schedule.LocalUserId),
            LocalUserName = _userNames.GetValueOrDefault(schedule.LocalUserId, ""),
            MilitaryUserName = _userNames.GetValueOrDefault(schedule.MilitaryUserId, ""),
        };

        // 역할에 따른 액션 텍스트 설정
        var currentUser = AuthService.CurrentUser;
        if (currentUser != null)
        {
            item.ActionText = GetActionText(schedule, currentUser);
            item.ActionIcon = GetActionIcon(schedule, currentUser);
            item.ShowConfirmStatus = schedule.Status == "reserved";
            item.CanDelete = currentUser.Role == "middle_military" && schedule.Status == "created";

            // 확정 상태 설정
            if (schedule.Status == "reserved")
            {
                item.LocalConfirmStatus = schedule.LocalConfirmed ? "✅" : "⏳";
                item.MilitaryConfirmStatus = schedule.MilitaryConfirmed ? "✅" : "⏳";

                // 현재 사용자 기준 확정 필요 여부
                if (currentUser.Role == "user_local")
                {
                    item.NeedsMyConfirm = !schedule.LocalConfirmed;
                }
                else if (currentUser.Role == "user_military")
                {
                    item.NeedsMyConfirm = !schedule.MilitaryConfirmed;
                }

                // 중간관리자용: 미확정자 표시
                if (currentUser.Role == "middle_military" || currentUser.Role == "middle_local")
                {
                    if (!schedule.LocalConfirmed && !schedule.MilitaryConfirmed)
                    {
                        item.UnconfirmedInfo = "⚠️ 양측 미확정";
                    }
                    else if (!schedule.LocalConfirmed)
                    {
                        item.UnconfirmedInfo = "⚠️ 지자체 미확정";
                    }
                    else if (!schedule.MilitaryConfirmed)
                    {
                        item.UnconfirmedInfo = "⚠️ 대대 미확정";
                    }
                }
            }
        }

        return item;
    }

    private string GetBattalionNameFromUser(Guid userId)
    {
        if (_userCache.TryGetValue(userId, out var user) && user.BattalionId.HasValue)
        {
            return _battalionNames.GetValueOrDefault(user.BattalionId.Value, "");
        }
        return "";
    }

    private string GetDistrictNameFromUser(Guid userId)
    {
        if (_userCache.TryGetValue(userId, out var user) && user.DistrictId.HasValue)
        {
            return _districtNames.GetValueOrDefault(user.DistrictId.Value, "");
        }
        return "";
    }

    private string GetActionText(Schedule schedule, User currentUser)
    {
        return (schedule.Status, currentUser.Role) switch
        {
            ("created", "user_local") => "📝 일정 입력하기",
            ("inputted", "user_military") => "📅 일정 예약하기",
            ("reserved", "user_local") when !schedule.LocalConfirmed => "✅ 확정 필요",
            ("reserved", "user_military") when !schedule.MilitaryConfirmed => "✅ 확정 필요",
            ("reserved", _) when schedule.LocalConfirmed && schedule.MilitaryConfirmed => "🔒 확정 완료",
            ("reserved", "user_local") when schedule.LocalConfirmed => "⏳ 상대방 대기",
            ("reserved", "user_military") when schedule.MilitaryConfirmed => "⏳ 상대방 대기",
            ("confirmed", _) => "📄 상세보기",
            ("created", "middle_military") => "🗑️ 삭제 가능",
            _ => "📄 상세보기"
        };
    }

    private string GetActionIcon(Schedule schedule, User currentUser)
    {
        return (schedule.Status, currentUser.Role) switch
        {
            ("created", "user_local") => "→",
            ("inputted", "user_military") => "→",
            ("reserved", _) => "→",
            ("confirmed", _) => "→",
            _ => "→"
        };
    }

    /// <summary>
    /// 상태 필터 변경
    /// </summary>
    [RelayCommand]
    private void SetStatusFilter(string status)
    {
        SelectedStatusFilter = status;
        ApplyFilter();
    }

    /// <summary>
    /// 일정 카드 클릭
    /// </summary>
    [RelayCommand]
    private void SelectSchedule(ScheduleListItem item)
    {
        if (item?.Schedule == null) return;

        var schedule = item.Schedule;
        var currentUser = AuthService.CurrentUser;
        if (currentUser == null) return;

        // 역할과 상태에 따라 다른 화면으로 이동
        var action = (schedule.Status, currentUser.Role) switch
        {
            ("created", "user_local") => "input",
            ("inputted", "user_military") => "reserve",
            ("reserved", _) => "detail",
            ("confirmed", _) => "detail",
            _ => "detail"
        };

        NavigateToScheduleDetail?.Invoke(schedule, action);
    }

    /// <summary>
    /// 일정 삭제 (사단담당자, 생성됨 상태만)
    /// </summary>
    [RelayCommand]
    private async Task DeleteScheduleAsync(ScheduleListItem item)
    {
        if (item?.Schedule == null) return;

        var schedule = item.Schedule;
        var currentUser = AuthService.CurrentUser;

        // 권한 확인
        if (currentUser?.Role != "middle_military" || schedule.Status != "created")
        {
            System.Diagnostics.Debug.WriteLine("[ScheduleListVM] Delete not allowed");
            return;
        }

        try
        {
            // Soft delete
            await SupabaseService.Client.From<Schedule>()
                .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, schedule.Id.ToString())
                .Set(s => s.DeletedAt, DateTime.UtcNow)
                .Set(s => s.DeletedBy, currentUser.Id)
                .Update();

            // 목록에서 제거
            _allSchedules.Remove(schedule);
            Schedules.Remove(item);
            UpdateStatusCounts();

            System.Diagnostics.Debug.WriteLine($"[ScheduleListVM] Schedule deleted: {schedule.Id}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScheduleListVM] Failed to delete schedule: {ex.Message}");
        }
    }

    /// <summary>
    /// 업체 등록 화면 열기
    /// </summary>
    [RelayCommand]
    private void OpenCompanyRegister()
    {
        NavigateToCompanyRegister?.Invoke();
    }

    /// <summary>
    /// 일정 생성 화면 열기
    /// </summary>
    [RelayCommand]
    private void OpenScheduleCreate()
    {
        NavigateToScheduleCreate?.Invoke();
    }

    /// <summary>
    /// 새로고침
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadCacheDataAsync();
        await LoadSchedulesAsync();
    }

    /// <summary>
    /// 캐시 정리
    /// </summary>
    public void ClearCache()
    {
        Schedules.Clear();
        _allSchedules.Clear();
        _companyNames.Clear();
        _battalionNames.Clear();
        _battalionCache.Clear();
        _districtNames.Clear();
        _districtCache.Clear();
        _userNames.Clear();
        _userCache.Clear();
    }
}

/// <summary>
/// 일정 목록 아이템 (UI 바인딩용)
/// </summary>
public partial class ScheduleListItem : ObservableObject
{
    public Schedule Schedule { get; set; } = null!;

    public string CompanyName { get; set; } = "";
    public string BattalionName { get; set; } = "";
    public string DistrictName { get; set; } = "";
    public string LocalUserName { get; set; } = "";
    public string MilitaryUserName { get; set; } = "";

    public string ActionText { get; set; } = "";
    public string ActionIcon { get; set; } = "→";

    public bool ShowConfirmStatus { get; set; }
    public string LocalConfirmStatus { get; set; } = "";
    public string MilitaryConfirmStatus { get; set; } = "";
    public bool NeedsMyConfirm { get; set; }
    public string UnconfirmedInfo { get; set; } = "";

    public bool CanDelete { get; set; }

    // 헬퍼 프로퍼티
    public string StatusDisplay => Schedule?.StatusDisplayName ?? "";
    public string StatusColor => Schedule?.StatusColor ?? "#9E9E9E";
    public string ReservedTimeDisplay => Schedule?.ReservedTimeDisplay ?? "";
    public bool HasReservedTime => !string.IsNullOrEmpty(ReservedTimeDisplay);
}
