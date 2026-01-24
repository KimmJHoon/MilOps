using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MilOps.Services;
using System;

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

    // 메뉴 표시 여부 (역할별)
    public bool ShowScheduleTab => IsUser || IsMiddleAdmin;  // 사용자, 중간관리자
    public bool ShowManagerTab => IsMiddleAdmin || IsSuperAdmin;  // 중간관리자, 최종관리자

    public event Action? LogoutRequested;

    public MainViewModel()
    {
        UpdateUserRole();
    }

    public void RefreshUserRole()
    {
        UpdateUserRole();
        CurrentUserId = AuthService.CurrentUserId ?? "";
        CurrentUserName = AuthService.CurrentUser?.Name ?? "";

        // 현재 선택된 탭이 해당 역할에서 접근 불가능하면 기본 탭(캘린더)으로 리셋
        ResetToValidTab();

        System.Diagnostics.Debug.WriteLine($"[MainViewModel] RefreshUserRole - IsSuperAdmin: {IsSuperAdmin}, IsMiddleAdmin: {IsMiddleAdmin}, IsUser: {IsUser}");
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
    private void Logout()
    {
        System.Diagnostics.Debug.WriteLine("[MainViewModel] Logout command started");
        AuthService.Logout();
        IsDrawerOpen = false;
        System.Diagnostics.Debug.WriteLine($"[MainViewModel] LogoutRequested has listeners: {LogoutRequested != null}");
        LogoutRequested?.Invoke();
        System.Diagnostics.Debug.WriteLine("[MainViewModel] Logout command completed");
    }
}
