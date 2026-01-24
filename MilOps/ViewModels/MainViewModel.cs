using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MilOps.Services;
using System;

namespace MilOps.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private int _selectedTabIndex = 0;

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

    [ObservableProperty]
    private bool _isSuperAdmin = false;

    [ObservableProperty]
    private string _currentUserId = "";

    [ObservableProperty]
    private string _currentUserName = "";

    public event Action? LogoutRequested;

    public MainViewModel()
    {
        // AuthService에서 현재 사용자 역할 확인
        _isSuperAdmin = AuthService.IsSuperAdmin;

        // 디버그 로그
        System.Diagnostics.Debug.WriteLine($"[MainViewModel] IsSuperAdmin: {_isSuperAdmin}");
        System.Diagnostics.Debug.WriteLine($"[MainViewModel] CurrentUserRole: {AuthService.CurrentUserRole}");
        System.Diagnostics.Debug.WriteLine($"[MainViewModel] CurrentUser: {AuthService.CurrentUser?.LoginId}");
    }

    public void RefreshUserRole()
    {
        IsSuperAdmin = AuthService.IsSuperAdmin;
        CurrentUserId = AuthService.CurrentUserId ?? "";
        CurrentUserName = AuthService.CurrentUser?.Name ?? "";
        System.Diagnostics.Debug.WriteLine($"[MainViewModel] RefreshUserRole - IsSuperAdmin: {IsSuperAdmin}, UserId: {CurrentUserId}");
    }

    [RelayCommand]
    private void SelectTab(string tabIndex)
    {
        if (int.TryParse(tabIndex, out int index))
        {
            SelectedTabIndex = index;

            IsCalendarSelected = false;
            IsScheduleSelected = false;
            IsManagerSelected = false;
            IsNotificationSelected = false;
            IsSettingsSelected = false;

            switch (index)
            {
                case 0:
                    IsCalendarSelected = true;
                    CurrentPageTitle = "캘린더";
                    break;
                case 1:
                    if (IsSuperAdmin)
                    {
                        IsManagerSelected = true;
                        CurrentPageTitle = "담당자 관리";
                    }
                    else
                    {
                        IsScheduleSelected = true;
                        CurrentPageTitle = "일정";
                    }
                    break;
                case 2:
                    IsNotificationSelected = true;
                    CurrentPageTitle = "알림";
                    break;
                case 3:
                    IsSettingsSelected = true;
                    CurrentPageTitle = "설정";
                    break;
            }
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
