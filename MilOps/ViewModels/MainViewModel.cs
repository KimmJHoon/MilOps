using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MilOps.Services;
using System;
using System.Threading.Tasks;

namespace MilOps.ViewModels;

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

            string regionName = "";

            // 지역 정보 로드 (role에 따라 다름)
            if (user.RegionId.HasValue)
            {
                var regionResult = await client
                    .From<Models.Region>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, user.RegionId.ToString())
                    .Single();
                if (regionResult != null)
                    regionName = regionResult.Name;
            }

            if (user.DistrictId.HasValue)
            {
                var districtResult = await client
                    .From<Models.District>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, user.DistrictId.ToString())
                    .Single();
                if (districtResult != null)
                    regionName += (string.IsNullOrEmpty(regionName) ? "" : " ") + districtResult.Name;
            }

            if (user.DivisionId.HasValue)
            {
                var divisionResult = await client
                    .From<Models.Division>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, user.DivisionId.ToString())
                    .Single();
                if (divisionResult != null)
                    regionName += (string.IsNullOrEmpty(regionName) ? "" : " ") + divisionResult.Name;
            }

            if (user.BattalionId.HasValue)
            {
                var battalionResult = await client
                    .From<Models.Battalion>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, user.BattalionId.ToString())
                    .Single();
                if (battalionResult != null)
                    regionName += (string.IsNullOrEmpty(regionName) ? "" : " ") + battalionResult.Name;
            }

            CurrentUserRegion = regionName;
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

    [RelayCommand]
    private async Task Logout()
    {
        System.Diagnostics.Debug.WriteLine("[MainViewModel] Logout command started - initiating app restart");
        IsDrawerOpen = false;

        // AppRestartService를 통해 로그아웃 및 앱 재시작
        await AppRestartService.LogoutAndRestartAsync();

        System.Diagnostics.Debug.WriteLine("[MainViewModel] Logout command completed");
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
