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

    public string CurrentUserId => AuthService.CurrentUserId ?? "";

    public event Action? LogoutRequested;

    public MainViewModel()
    {
        _isSuperAdmin = AuthService.IsSuperAdmin;
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
        AuthService.Logout();
        IsDrawerOpen = false;
        LogoutRequested?.Invoke();
    }
}
