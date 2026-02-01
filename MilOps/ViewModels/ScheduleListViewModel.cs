using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MilOps.Models;
using MilOps.Services;
using MilOps.Services.Abstractions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace MilOps.ViewModels;

public partial class ScheduleListViewModel : ViewModelBase
{
    // 의존성 주입을 위한 서비스
    private readonly IAuthService _authService;
    private readonly ISupabaseService _supabaseService;

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

    // 삭제 확인 모달
    [ObservableProperty]
    private bool _showDeleteModal = false;

    [ObservableProperty]
    private string _deleteModalCompanyName = "";

    [ObservableProperty]
    private string _deleteModalBattalionName = "";

    private ScheduleListItem? _pendingDeleteItem;

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

    /// <summary>
    /// 기본 생성자 - 프로덕션용 (기존 코드 호환)
    /// </summary>
    public ScheduleListViewModel()
        : this(new AuthServiceAdapter(), new SupabaseServiceAdapter())
    {
    }

    /// <summary>
    /// DI 생성자 - 테스트용
    /// </summary>
    public ScheduleListViewModel(IAuthService authService, ISupabaseService supabaseService, bool autoInitialize = true)
    {
        _authService = authService;
        _supabaseService = supabaseService;

        if (autoInitialize)
        {
            _ = InitializeAsync();
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            // 로그인되지 않은 상태면 초기화 중단
            if (_authService.CurrentUser == null)
            {
                System.Diagnostics.Debug.WriteLine("[ScheduleListVM] InitializeAsync skipped - no current user");
                return;
            }

            DetermineUserRole();
            await LoadCacheDataAsync();
            await LoadSchedulesAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScheduleListVM] InitializeAsync error: {ex.Message}");
        }
    }

    /// <summary>
    /// 현재 사용자 역할 판단 및 UI 설정
    /// </summary>
    private void DetermineUserRole()
    {
        var currentUser = _authService.CurrentUser;
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
        if (!_supabaseService.IsInitialized) return;

        try
        {
            var companiesTask = _supabaseService.GetActiveCompaniesAsync();
            var battalionsTask = _supabaseService.GetBattalionsAsync();
            var districtsTask = _supabaseService.GetDistrictsAsync();
            var usersTask = _supabaseService.GetActiveUsersAsync();

            await Task.WhenAll(companiesTask, battalionsTask, districtsTask, usersTask);

            _companyNames = companiesTask.Result.ToDictionary(c => c.Id, c => c.Name);
            _battalionNames = battalionsTask.Result.ToDictionary(b => b.Id, b => b.Name);
            _battalionCache = battalionsTask.Result.ToDictionary(b => b.Id, b => b);
            _districtNames = districtsTask.Result.ToDictionary(d => d.Id, d => d.Name);
            _districtCache = districtsTask.Result.ToDictionary(d => d.Id, d => d);
            _userNames = usersTask.Result.ToDictionary(u => u.Id, u => u.FullDisplayName);
            _userCache = usersTask.Result.ToDictionary(u => u.Id, u => u);

            // 현재 사용자 표시 갱신
            if (_authService.CurrentUser != null)
            {
                UpdateCurrentUserDisplay(_authService.CurrentUser);
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
        if (!_supabaseService.IsInitialized) return;
        if (_authService.CurrentUser == null) return;

        IsLoading = true;
        try
        {
            var currentUser = _authService.CurrentUser;

            // 모든 일정을 가져온 후 클라이언트에서 필터링
            var schedules = await _supabaseService.GetSchedulesAsync();

            // 삭제되지 않은 일정만 필터링
            _allSchedules = schedules.Where(s => !s.IsDeleted).ToList();

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
            StatusDisplay = schedule.StatusDisplayName,
            StatusColor = schedule.StatusColor,
        };

        // 역할에 따른 액션 텍스트 설정
        var currentUser = _authService.CurrentUser;
        if (currentUser != null)
        {
            item.ActionText = GetActionText(schedule, currentUser);
            item.ActionIcon = GetActionIcon(schedule, currentUser);
            item.ShowConfirmStatus = schedule.Status == "reserved";
            // 사단담당자가 생성됨 상태의 일정만 삭제 가능
            item.CanDelete = currentUser.Role == "middle_military"
                && schedule.Status == "created";

            System.Diagnostics.Debug.WriteLine($"[ScheduleListVM] CreateScheduleListItem - Schedule: {schedule.Id}, Status: {schedule.Status}, Role: {currentUser.Role}, CanDelete: {item.CanDelete}");

            // 상태별 미확정 정보 표시
            if (schedule.Status == "created")
            {
                // 생성됨: 양측 미확정
                item.UnconfirmedInfo = "양측 미확정";
            }
            else if (schedule.Status == "inputted")
            {
                // 입력됨: 사용자 역할에 따라 표시
                if (currentUser.Role == "user_local" || currentUser.Role == "middle_local" || currentUser.Role == "super_admin_mois")
                {
                    item.UnconfirmedInfo = "대대 미확정";
                }
                else if (currentUser.Role == "user_military" || currentUser.Role == "middle_military" || currentUser.Role == "super_admin_army")
                {
                    item.UnconfirmedInfo = "지자체 미확정";
                }
            }
            else if (schedule.Status == "reserved")
            {
                // 예약됨: 표시 없음 (확정 대기 상태)
                // 현재 사용자 기준 확정 필요 여부만 설정
                if (currentUser.Role == "user_local")
                {
                    item.NeedsMyConfirm = !schedule.LocalConfirmed;
                }
                else if (currentUser.Role == "user_military")
                {
                    item.NeedsMyConfirm = !schedule.MilitaryConfirmed;
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
            ("created", "user_local") => "일정 입력하기",
            ("inputted", "user_military") => "일정 예약하기",
            ("reserved", "user_local") when !schedule.LocalConfirmed => "확정 필요",
            ("reserved", "user_military") when !schedule.MilitaryConfirmed => "확정 필요",
            ("reserved", _) when schedule.LocalConfirmed && schedule.MilitaryConfirmed => "확정 완료",
            ("reserved", "user_local") when schedule.LocalConfirmed => "상대방 대기",
            ("reserved", "user_military") when schedule.MilitaryConfirmed => "상대방 대기",
            ("confirmed", _) => "상세보기",
            ("created", "middle_military") => "삭제하기",
            _ => "상세보기"
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
        var currentUser = _authService.CurrentUser;
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
    /// 일정 삭제 모달 표시 (사단담당자, 생성됨 상태만)
    /// </summary>
    [RelayCommand]
    private void DeleteSchedule(ScheduleListItem item)
    {
        if (item?.Schedule == null) return;

        var schedule = item.Schedule;
        var currentUser = _authService.CurrentUser;

        // 권한 확인: 사단담당자가 생성됨 상태의 일정만 삭제 가능
        if (currentUser?.Role != "middle_military" || schedule.Status != "created")
        {
            System.Diagnostics.Debug.WriteLine($"[ScheduleListVM] Delete not allowed - Role: {currentUser?.Role}, Status: {schedule.Status}");
            return;
        }

        // 삭제 확인 모달 표시
        _pendingDeleteItem = item;
        DeleteModalCompanyName = item.CompanyName;
        DeleteModalBattalionName = item.BattalionName;
        ShowDeleteModal = true;
    }

    /// <summary>
    /// 삭제 확인 (모달에서 확인 버튼 클릭)
    /// </summary>
    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        ShowDeleteModal = false;

        if (_pendingDeleteItem?.Schedule == null) return;

        var schedule = _pendingDeleteItem.Schedule;
        var currentUser = _authService.CurrentUser;

        if (currentUser == null) return;

        try
        {
            // Soft delete
            await _supabaseService.SoftDeleteScheduleAsync(schedule.Id, currentUser.Id);

            // 목록에서 제거
            _allSchedules.Remove(schedule);
            Schedules.Remove(_pendingDeleteItem);
            UpdateStatusCounts();

            System.Diagnostics.Debug.WriteLine($"[ScheduleListVM] Schedule deleted: {schedule.Id}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScheduleListVM] Failed to delete schedule: {ex.Message}");
        }
        finally
        {
            _pendingDeleteItem = null;
        }
    }

    /// <summary>
    /// 삭제 취소 (모달에서 취소 버튼 클릭)
    /// </summary>
    [RelayCommand]
    private void CancelDelete()
    {
        ShowDeleteModal = false;
        _pendingDeleteItem = null;
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
        // 모달 닫기
        ShowDeleteModal = false;
        _pendingDeleteItem = null;

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

    /// <summary>
    /// 특정 일정의 상태를 직접 업데이트 (리프레시 없이)
    /// </summary>
    public void UpdateScheduleStatus(Guid scheduleId, string newStatus, int newStatusOrder)
    {
        System.Diagnostics.Debug.WriteLine($"[ScheduleListVM] UpdateScheduleStatus - id: {scheduleId}, status: {newStatus}, order: {newStatusOrder}");

        // _allSchedules에서 해당 일정 찾아서 업데이트
        var schedule = _allSchedules.FirstOrDefault(s => s.Id == scheduleId);
        if (schedule != null)
        {
            schedule.Status = newStatus;
            schedule.StatusOrder = newStatusOrder;
            System.Diagnostics.Debug.WriteLine($"[ScheduleListVM] Updated schedule in _allSchedules");
        }

        // Schedules 컬렉션에서 해당 아이템 찾아서 UI 갱신
        var item = Schedules.FirstOrDefault(s => s.Schedule?.Id == scheduleId);
        if (item != null)
        {
            item.Schedule.Status = newStatus;
            item.Schedule.StatusOrder = newStatusOrder;
            item.UpdateStatusDisplay();

            // ActionText도 갱신
            var currentUser = _authService.CurrentUser;
            if (currentUser != null)
            {
                item.ActionText = GetActionText(item.Schedule, currentUser);
            }

            System.Diagnostics.Debug.WriteLine($"[ScheduleListVM] Updated UI item - StatusDisplay: {item.StatusDisplay}");
        }

        // 상태별 카운트 갱신
        UpdateStatusCounts();
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

    [ObservableProperty]
    private string _actionText = "";

    public string ActionIcon { get; set; } = "→";

    public bool ShowConfirmStatus { get; set; }
    public string LocalConfirmStatus { get; set; } = "";
    public string MilitaryConfirmStatus { get; set; } = "";
    public bool NeedsMyConfirm { get; set; }
    public string UnconfirmedInfo { get; set; } = "";

    public bool CanDelete { get; set; }

    // 헬퍼 프로퍼티 (ObservableProperty로 변경하여 UI 갱신 지원)
    [ObservableProperty]
    private string _statusDisplay = "";

    [ObservableProperty]
    private string _statusColor = "#9E9E9E";

    public string ReservedTimeDisplay => Schedule?.ReservedTimeDisplay ?? "";
    public bool HasReservedTime => !string.IsNullOrEmpty(ReservedTimeDisplay);

    /// <summary>
    /// Schedule 상태 변경 시 UI 속성 갱신
    /// </summary>
    public void UpdateStatusDisplay()
    {
        if (Schedule == null) return;
        StatusDisplay = Schedule.StatusDisplayName;
        StatusColor = Schedule.StatusColor;
    }
}
