using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MilOps.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MilOps.ViewModels;

/// <summary>
/// 일정 화면 분기 타입
/// </summary>
public enum ScheduleNavigationType
{
    Input,      // 지자체담당자용 입력 화면
    Reserve,    // 대대담당자용 예약 화면
    Confirm     // 양측 확정 화면
}

/// <summary>
/// 일정 화면 이동 요청 인자
/// </summary>
public class ScheduleNavigationArgs
{
    public Guid ScheduleId { get; set; }
    public ScheduleNavigationType NavigationType { get; set; }
    public string Mode { get; set; } = "view";
}

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _selectedTab = "calendar";

    [ObservableProperty]
    private string _currentPageTitle = "캘린더";

    [ObservableProperty]
    private bool _isCalendarSelected = true;

    [ObservableProperty]
    private bool _isScheduleSelected = false;

    [ObservableProperty]
    private bool _isManagerSelected = false;

    [ObservableProperty]
    private bool _isNotificationSelected = false;

    [ObservableProperty]
    private bool _isSettingsSelected = false;

    [ObservableProperty]
    private bool _isDrawerOpen = false;

    // 역할별 플래그
    [ObservableProperty]
    private bool _isSuperAdmin = false;

    [ObservableProperty]
    private bool _isMiddleAdmin = false;

    [ObservableProperty]
    private bool _isUser = false;

    [ObservableProperty]
    private string _currentUserId = "";

    [ObservableProperty]
    private string _currentUserName = "";

    [ObservableProperty]
    private string _currentUserPhone = "";

    [ObservableProperty]
    private string _currentUserRole = "";

    [ObservableProperty]
    private string _currentUserPosition = "";

    [ObservableProperty]
    private string _currentUserRegion = "";

    // 오버레이 화면 열림 상태
    [ObservableProperty]
    private bool _isCompanyRegisterOpen = false;

    [ObservableProperty]
    private bool _isScheduleCreateOpen = false;

    [ObservableProperty]
    private bool _isScheduleInputOpen = false;

    [ObservableProperty]
    private bool _isScheduleReserveOpen = false;

    [ObservableProperty]
    private bool _isScheduleConfirmOpen = false;

    // 일정 입력/예약/확정 화면에 전달할 데이터
    private Guid _scheduleInputId;
    private Guid _scheduleReserveId;
    private Guid _scheduleConfirmId;

    public Guid ScheduleInputId => _scheduleInputId;
    public Guid ScheduleReserveId => _scheduleReserveId;
    public Guid ScheduleConfirmId => _scheduleConfirmId;

    // 메뉴 표시 여부 (역할별)
    public bool ShowScheduleTab => IsUser || IsMiddleAdmin;  // 사용자, 중간관리자
    public bool ShowManagerTab => IsMiddleAdmin || IsSuperAdmin;  // 중간관리자, 최종관리자

    public MainViewModel()
    {
        UpdateUserRole();
    }

    public void RefreshUserRole()
    {
        UpdateUserRole();
        var user = AuthService.CurrentUser;

        CurrentUserId = user?.LoginId ?? "";
        CurrentUserName = user?.Name ?? "";
        CurrentUserPhone = user?.Phone ?? "";
        CurrentUserRole = user?.RoleDisplayName ?? "";
        CurrentUserPosition = user?.PositionDisplay ?? "";

        // 지역 정보 로드 (비동기)
        _ = LoadUserRegionAsync();

        // 현재 선택된 탭이 해당 역할에서 접근 불가능하면 기본 탭(캘린더)으로 리셋
        ResetToValidTab();

        System.Diagnostics.Debug.WriteLine($"[MainViewModel] RefreshUserRole - IsSuperAdmin: {IsSuperAdmin}, IsMiddleAdmin: {IsMiddleAdmin}, IsUser: {IsUser}");
    }

    private async Task LoadUserRegionAsync()
    {
        try
        {
            var user = AuthService.CurrentUser;
            if (user == null) return;

            var client = SupabaseService.Client;
            if (client == null) return;

            // 4개 쿼리를 병렬로 실행 (순차 실행 대비 4배 빠름)
            var regionTask = user.RegionId.HasValue
                ? client.From<Models.Region>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, user.RegionId.ToString())
                    .Single()
                : Task.FromResult<Models.Region?>(null);

            var districtTask = user.DistrictId.HasValue
                ? client.From<Models.District>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, user.DistrictId.ToString())
                    .Single()
                : Task.FromResult<Models.District?>(null);

            var divisionTask = user.DivisionId.HasValue
                ? client.From<Models.Division>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, user.DivisionId.ToString())
                    .Single()
                : Task.FromResult<Models.Division?>(null);

            var battalionTask = user.BattalionId.HasValue
                ? client.From<Models.Battalion>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, user.BattalionId.ToString())
                    .Single()
                : Task.FromResult<Models.Battalion?>(null);

            // 모든 쿼리 병렬 대기
            await Task.WhenAll(regionTask, districtTask, divisionTask, battalionTask);

            // 결과 조합
            var parts = new List<string>();

            if (regionTask.Result != null)
                parts.Add(regionTask.Result.Name);

            if (districtTask.Result != null)
                parts.Add(districtTask.Result.Name);

            if (divisionTask.Result != null)
                parts.Add(divisionTask.Result.Name);

            if (battalionTask.Result != null)
                parts.Add(battalionTask.Result.Name);

            CurrentUserRegion = string.Join(" ", parts);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] LoadUserRegionAsync error: {ex.Message}");
        }
    }

    /// <summary>
    /// 현재 역할에서 접근 불가능한 탭이 선택되어 있으면 캘린더로 리셋
    /// </summary>
    private void ResetToValidTab()
    {
        // 담당자 탭은 중간관리자, 최종관리자만 접근 가능
        if (IsManagerSelected && !ShowManagerTab)
        {
            System.Diagnostics.Debug.WriteLine("[MainViewModel] Manager tab not accessible, resetting to calendar");
            SelectTab("calendar");
            return;
        }

        // 일정 탭은 사용자, 중간관리자만 접근 가능
        if (IsScheduleSelected && !ShowScheduleTab)
        {
            System.Diagnostics.Debug.WriteLine("[MainViewModel] Schedule tab not accessible, resetting to calendar");
            SelectTab("calendar");
            return;
        }
    }

    private void UpdateUserRole()
    {
        var role = AuthService.CurrentUserRole;
        var roleString = AuthService.CurrentUser?.Role ?? "null";

        IsSuperAdmin = AuthService.IsSuperAdmin;
        IsMiddleAdmin = role == UserRole.MiddleLocal || role == UserRole.MiddleMilitary;
        IsUser = role == UserRole.UserLocal || role == UserRole.UserMilitary;

        // 역할이 None인 경우 기본값으로 사용자 처리 (담당자 메뉴 숨김)
        if (role == UserRole.None && AuthService.IsLoggedIn)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] WARNING: Role is None but user is logged in. DB role string: {roleString}");
            // 기본적으로 가장 낮은 권한으로 처리
            IsUser = true;
        }

        // 속성 변경 알림 (메뉴 표시 여부 갱신)
        OnPropertyChanged(nameof(ShowScheduleTab));
        OnPropertyChanged(nameof(ShowManagerTab));

        System.Diagnostics.Debug.WriteLine($"[MainViewModel] UpdateUserRole - DBRole: {roleString}, ParsedRole: {role}, IsSuperAdmin: {IsSuperAdmin}, IsMiddleAdmin: {IsMiddleAdmin}, IsUser: {IsUser}");
    }

    // 탭 변경 이벤트 (View에서 구독하여 초기화 처리)
    public event Action<string>? TabChanged;

    [RelayCommand]
    private void SelectTab(string tabName)
    {
        SelectedTab = tabName;

        // 모든 탭 선택 해제
        IsCalendarSelected = false;
        IsScheduleSelected = false;
        IsManagerSelected = false;
        IsNotificationSelected = false;
        IsSettingsSelected = false;

        // 선택된 탭 활성화
        switch (tabName)
        {
            case "calendar":
                IsCalendarSelected = true;
                CurrentPageTitle = "캘린더";
                break;
            case "schedule":
                IsScheduleSelected = true;
                CurrentPageTitle = "일정";
                break;
            case "notification":
                IsNotificationSelected = true;
                CurrentPageTitle = "알림";
                break;
            case "manager":
                IsManagerSelected = true;
                CurrentPageTitle = "담당자 관리";
                break;
            case "settings":
                IsSettingsSelected = true;
                CurrentPageTitle = "설정";
                break;
        }

        // 탭 변경 이벤트 발생
        TabChanged?.Invoke(tabName);
    }

    [RelayCommand]
    private void ToggleDrawer()
    {
        IsDrawerOpen = !IsDrawerOpen;
    }

    [RelayCommand]
    private void CloseDrawer()
    {
        IsDrawerOpen = false;
    }

    // 로그아웃 완료 이벤트
    public event Action? LogoutCompleted;

    [RelayCommand]
    private async Task Logout()
    {
        System.Diagnostics.Debug.WriteLine("[MainViewModel] Logout command started");
        IsDrawerOpen = false;

        try
        {
            // 1. 정리 작업 (타이머, Realtime 구독 해제)
            try
            {
                AppRestartService.CleanupBeforeLogout?.Invoke();
                System.Diagnostics.Debug.WriteLine("[MainViewModel] Cleanup completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] Cleanup error (continuing): {ex.Message}");
            }

            // 2. 세션 저장소 클리어
            SessionStorageService.ClearSession();
            System.Diagnostics.Debug.WriteLine("[MainViewModel] Session cleared");

            // 3. AuthService 로그아웃
            await AuthService.LogoutAsync();
            System.Diagnostics.Debug.WriteLine("[MainViewModel] AuthService logged out");

            // 4. 로그아웃 완료 이벤트 발생 (UI에서 로그인 화면으로 전환)
            LogoutCompleted?.Invoke();
            System.Diagnostics.Debug.WriteLine("[MainViewModel] LogoutCompleted event invoked");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] Logout error: {ex.Message}");
        }
    }

    // === 업체 등록 화면 ===
    [RelayCommand]
    private void OpenCompanyRegister()
    {
        IsCompanyRegisterOpen = true;
        System.Diagnostics.Debug.WriteLine("[MainViewModel] OpenCompanyRegister");
    }

    [RelayCommand]
    private void CloseCompanyRegister()
    {
        IsCompanyRegisterOpen = false;
        System.Diagnostics.Debug.WriteLine("[MainViewModel] CloseCompanyRegister");
    }

    // === 일정 생성 화면 ===
    [RelayCommand]
    private void OpenScheduleCreate()
    {
        IsScheduleCreateOpen = true;
        System.Diagnostics.Debug.WriteLine("[MainViewModel] OpenScheduleCreate");
    }

    [RelayCommand]
    private void CloseScheduleCreate()
    {
        IsScheduleCreateOpen = false;
        System.Diagnostics.Debug.WriteLine("[MainViewModel] CloseScheduleCreate");
    }

    // === 일정 상세 화면 열기 (역할/상태에 따라 분기) ===

    /// <summary>
    /// 일정 상세 화면 열기 전 이벤트 (View에서 구독하여 실제 화면 전환 처리)
    /// </summary>
    public event Action<ScheduleNavigationArgs>? ScheduleNavigationRequested;

    /// <summary>
    /// 일정 상세 화면 열기 요청 (MVVM 패턴: View에서 직접 DB 호출하지 않고 ViewModel에서 처리)
    /// 백그라운드에서 일정 상태를 조회하고, 역할/상태에 따라 적절한 화면으로 분기
    /// </summary>
    public void RequestOpenScheduleDetail(Guid scheduleId, string mode = "view")
    {
        System.Diagnostics.Debug.WriteLine($"[MainViewModel] RequestOpenScheduleDetail - scheduleId: {scheduleId}, mode: {mode}");

        // 백그라운드에서 일정 조회 및 화면 분기 처리
        _ = Task.Run(async () =>
        {
            try
            {
                var currentUser = AuthService.CurrentUser;
                if (currentUser == null)
                {
                    System.Diagnostics.Debug.WriteLine("[MainViewModel] RequestOpenScheduleDetail - No user logged in");
                    return;
                }

                var client = SupabaseService.Client;
                if (client == null)
                {
                    System.Diagnostics.Debug.WriteLine("[MainViewModel] RequestOpenScheduleDetail - No Supabase client");
                    return;
                }

                // 백그라운드에서 일정 조회
                var schedule = await client.From<Models.Schedule>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, scheduleId.ToString())
                    .Single();

                if (schedule == null)
                {
                    System.Diagnostics.Debug.WriteLine("[MainViewModel] RequestOpenScheduleDetail - Schedule not found");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[MainViewModel] RequestOpenScheduleDetail - Schedule status: {schedule.Status}, role: {currentUser.Role}");

                // 화면 분기 로직 (비즈니스 로직)
                ScheduleNavigationType navigationType;

                // 예약됨/확정됨 상태 -> 확정 화면
                if (schedule.Status == "reserved" || schedule.Status == "confirmed")
                {
                    navigationType = ScheduleNavigationType.Confirm;
                }
                // 중간관리자/최종관리자는 모든 상태의 일정을 확정 화면(뷰어 모드)으로 조회
                else if (currentUser.Role == "middle_local" || currentUser.Role == "middle_military" ||
                         currentUser.Role == "super_admin_mois" || currentUser.Role == "super_admin_army")
                {
                    navigationType = ScheduleNavigationType.Confirm;
                }
                // 역할에 따라 분기
                else if (currentUser.Role == "user_local")
                {
                    // 지자체담당자 -> 일정 입력 화면
                    navigationType = ScheduleNavigationType.Input;
                }
                else if (currentUser.Role == "user_military")
                {
                    // 대대담당자 -> 입력됨 상태일 때만 예약 화면
                    if (schedule.Status == "inputted")
                    {
                        navigationType = ScheduleNavigationType.Reserve;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[MainViewModel] Schedule not ready for reservation (status: {schedule.Status})");
                        return;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[MainViewModel] Unsupported role: {currentUser.Role}");
                    return;
                }

                // UI 스레드에서 화면 전환 이벤트 발생
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    ScheduleNavigationRequested?.Invoke(new ScheduleNavigationArgs
                    {
                        ScheduleId = scheduleId,
                        NavigationType = navigationType,
                        Mode = mode
                    });
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] RequestOpenScheduleDetail error: {ex.Message}");
            }
        });
    }

    // === 일정 입력 화면 (지자체담당자용) ===
    public void OpenScheduleInput(Guid scheduleId)
    {
        _scheduleInputId = scheduleId;
        IsScheduleInputOpen = true;
        System.Diagnostics.Debug.WriteLine($"[MainViewModel] OpenScheduleInput - scheduleId: {scheduleId}");
    }

    [RelayCommand]
    private void CloseScheduleInput()
    {
        IsScheduleInputOpen = false;
        System.Diagnostics.Debug.WriteLine("[MainViewModel] CloseScheduleInput");
    }

    // === 일정 예약 화면 (대대담당자용) ===
    public void OpenScheduleReserve(Guid scheduleId)
    {
        _scheduleReserveId = scheduleId;
        IsScheduleReserveOpen = true;
        System.Diagnostics.Debug.WriteLine($"[MainViewModel] OpenScheduleReserve - scheduleId: {scheduleId}");
    }

    [RelayCommand]
    private void CloseScheduleReserve()
    {
        IsScheduleReserveOpen = false;
        System.Diagnostics.Debug.WriteLine("[MainViewModel] CloseScheduleReserve");
    }

    // === 일정 확정 화면 (양측 공통) ===
    public void OpenScheduleConfirm(Guid scheduleId)
    {
        _scheduleConfirmId = scheduleId;
        IsScheduleConfirmOpen = true;
        System.Diagnostics.Debug.WriteLine($"[MainViewModel] OpenScheduleConfirm - scheduleId: {scheduleId}");
    }

    [RelayCommand]
    private void CloseScheduleConfirm()
    {
        IsScheduleConfirmOpen = false;
        System.Diagnostics.Debug.WriteLine("[MainViewModel] CloseScheduleConfirm");
    }
}
