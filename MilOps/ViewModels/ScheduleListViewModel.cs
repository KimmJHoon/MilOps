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

    // 이벤트 구독 해제용
    private bool _isSubscribed = false;

    // 일정 목록 (배치 업데이트를 위해 setter 추가)
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

        // ScheduleDataService 이벤트 구독
        SubscribeToDataService();

        if (autoInitialize)
        {
            _ = InitializeAsync();
        }
    }

    /// <summary>
    /// ScheduleDataService 이벤트 구독
    /// </summary>
    private void SubscribeToDataService()
    {
        if (_isSubscribed) return;

        ScheduleDataService.DataLoaded += OnDataLoaded;
        ScheduleDataService.LoadingStateChanged += OnLoadingStateChanged;
        ScheduleDataService.CacheLoaded += OnCacheLoaded;
        _isSubscribed = true;
    }

    /// <summary>
    /// 이벤트 구독 해제 (Dispose 시 호출)
    /// </summary>
    public void UnsubscribeFromDataService()
    {
        if (!_isSubscribed) return;

        ScheduleDataService.DataLoaded -= OnDataLoaded;
        ScheduleDataService.LoadingStateChanged -= OnLoadingStateChanged;
        ScheduleDataService.CacheLoaded -= OnCacheLoaded;
        _isSubscribed = false;
    }

    /// <summary>
    /// 데이터 로드 완료 이벤트 핸들러 (백그라운드 -> UI 스레드)
    /// </summary>
    private void OnDataLoaded(ScheduleDataLoadedEventArgs args)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // 원본 데이터 저장
            _allSchedules = args.Schedules;

            // 상태별 카운트 업데이트
            AllCount = args.StatusCounts.GetValueOrDefault("all", 0);
            CreatedCount = args.StatusCounts.GetValueOrDefault("created", 0);
            InputtedCount = args.StatusCounts.GetValueOrDefault("inputted", 0);
            ReservedCount = args.StatusCounts.GetValueOrDefault("reserved", 0);
            ConfirmedCount = args.StatusCounts.GetValueOrDefault("confirmed", 0);

            // 필터 적용 및 UI 업데이트 (배치 방식)
            ApplyFilterWithItems(args.Items);
        });
    }

    /// <summary>
    /// 로딩 상태 변경 이벤트 핸들러
    /// Optimistic UI: 캐시된 데이터가 있으면 로딩 화면을 보여주지 않음
    /// </summary>
    private void OnLoadingStateChanged(bool isLoading)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // 캐시된 데이터가 있으면 로딩 화면을 보여주지 않음 (SWR 패턴)
            if (isLoading && ScheduleDataService.HasCachedResult)
            {
                // 캐시 데이터를 이미 표시 중이므로 로딩 화면 불필요
                return;
            }
            IsLoading = isLoading;
        });
    }

    /// <summary>
    /// 캐시 로드 완료 이벤트 핸들러
    /// </summary>
    private void OnCacheLoaded(ScheduleCacheLoadedEventArgs args)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // 캐시 데이터를 로컬 딕셔너리에 복사 (기존 코드 호환성)
            _companyNames = ScheduleDataService.CompanyNames.ToDictionary(kv => kv.Key, kv => kv.Value);
            _battalionNames = ScheduleDataService.BattalionNames.ToDictionary(kv => kv.Key, kv => kv.Value);
            _districtNames = ScheduleDataService.DistrictNames.ToDictionary(kv => kv.Key, kv => kv.Value);
            _userNames = ScheduleDataService.UserNames.ToDictionary(kv => kv.Key, kv => kv.Value);
            _userCache = ScheduleDataService.UserCache.ToDictionary(kv => kv.Key, kv => kv.Value);
            _battalionCache = ScheduleDataService.BattalionCache.ToDictionary(kv => kv.Key, kv => kv.Value);
            _districtCache = ScheduleDataService.DistrictCache.ToDictionary(kv => kv.Key, kv => kv.Value);

            // 현재 사용자 표시 갱신
            if (_authService.CurrentUser != null)
            {
                UpdateCurrentUserDisplay(_authService.CurrentUser);
            }
        });
    }

    private Task InitializeAsync()
    {
        try
        {
            // 로그인되지 않은 상태면 초기화 중단
            if (_authService.CurrentUser == null)
            {
                return Task.CompletedTask;
            }

            DetermineUserRole();

            // 이미 캐시된 데이터가 있으면 Preload에서 로드한 것 사용
            // LoadSchedulesInBackground는 캐시가 있으면 즉시 반환 (0ms)
            ScheduleDataService.LoadSchedulesInBackground(_authService.CurrentUser);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScheduleListVM] InitializeAsync error: {ex.Message}");
        }
        return Task.CompletedTask;
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
                GuideMessage = "생성됨 상태의 일정을 클릭하여\n가능 일정을 입력해주세요";
                ShowGuideMessage = true;
                break;

            case "user_military": // 대대담당자
                ShowMilitaryUserTab = true;
                SelectedRoleTab = "user_military";
                GuideMessage = "입력됨 상태의 일정을 클릭하여\n예약해주세요";
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

        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScheduleListVM] Failed to load cache: {ex.Message}");
        }
    }

    /// <summary>
    /// 일정 목록 로드 (ScheduleDataService를 통해 백그라운드에서 실행)
    /// </summary>
    [RelayCommand]
    public async Task LoadSchedulesAsync()
    {
        if (_authService.CurrentUser == null) return;

        // ScheduleDataService를 통해 백그라운드에서 데이터 로드
        // 결과는 OnDataLoaded 이벤트 핸들러에서 처리됨
        ScheduleDataService.LoadSchedulesInBackground(_authService.CurrentUser);

        // 비동기 메서드 시그니처 유지를 위한 await (실제로는 fire-and-forget)
        await Task.CompletedTask;
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
    /// 상태 필터 적용 (기존 방식 - 호환성 유지)
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

        // 최신순 정렬 (생성일 내림차순)
        var sorted = filtered.OrderByDescending(s => s.CreatedAt);

        // 배치 업데이트: 한 번에 컬렉션 교체 (N+1 UI 업데이트 -> 1회)
        var items = sorted.Select(s => CreateScheduleListItem(s)).ToList();
        Schedules = new ObservableCollection<ScheduleListItem>(items);

        ShowEmptyMessage = Schedules.Count == 0;
        UpdateEmptyMessage();
    }

    /// <summary>
    /// 상태 필터 적용 (미리 생성된 아이템 사용 - ScheduleDataService에서 전달)
    /// </summary>
    private void ApplyFilterWithItems(List<ScheduleListItem> allItems)
    {
        var filtered = SelectedStatusFilter switch
        {
            "created" => allItems.Where(i => i.Schedule?.Status == "created"),
            "inputted" => allItems.Where(i => i.Schedule?.Status == "inputted"),
            "reserved" => allItems.Where(i => i.Schedule?.Status == "reserved"),
            "confirmed" => allItems.Where(i => i.Schedule?.Status == "confirmed"),
            _ => allItems
        };

        // 배치 업데이트: 한 번에 컬렉션 교체 (1회 UI 업데이트)
        Schedules = new ObservableCollection<ScheduleListItem>(filtered);

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
        if (_authService.CurrentUser == null) return;

        // ScheduleDataService를 통해 백그라운드에서 데이터 로드
        ScheduleDataService.LoadSchedulesInBackground(_authService.CurrentUser);
        await Task.CompletedTask;
    }

    /// <summary>
    /// 캐시 정리 및 이벤트 구독 해제
    /// </summary>
    public void ClearCache()
    {
        // 이벤트 구독 해제
        UnsubscribeFromDataService();

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
        // _allSchedules에서 해당 일정 찾아서 업데이트
        var schedule = _allSchedules.FirstOrDefault(s => s.Id == scheduleId);
        if (schedule != null)
        {
            schedule.Status = newStatus;
            schedule.StatusOrder = newStatusOrder;
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
