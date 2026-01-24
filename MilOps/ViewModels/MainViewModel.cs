using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MilOps.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private int _selectedTabIndex = 0;

    [ObservableProperty]
    private string _currentPageTitle = "홈";

    // 각 탭 선택 상태
    [ObservableProperty]
    private bool _isHomeSelected = true;

    [ObservableProperty]
    private bool _isCalendarSelected = false;

    [ObservableProperty]
    private bool _isScheduleSelected = false;

    [ObservableProperty]
    private bool _isNotificationSelected = false;

    [ObservableProperty]
    private bool _isSettingsSelected = false;

    [RelayCommand]
    private void SelectTab(string tabIndex)
    {
        if (int.TryParse(tabIndex, out int index))
        {
            SelectedTabIndex = index;

            // 모든 탭 선택 해제
            IsHomeSelected = false;
            IsCalendarSelected = false;
            IsScheduleSelected = false;
            IsNotificationSelected = false;
            IsSettingsSelected = false;

            // 선택된 탭만 활성화
            switch (index)
            {
                case 0:
                    IsHomeSelected = true;
                    CurrentPageTitle = "홈";
                    break;
                case 1:
                    IsCalendarSelected = true;
                    CurrentPageTitle = "캘린더";
                    break;
                case 2:
                    IsScheduleSelected = true;
                    CurrentPageTitle = "일정";
                    break;
                case 3:
                    IsNotificationSelected = true;
                    CurrentPageTitle = "알림";
                    break;
                case 4:
                    IsSettingsSelected = true;
                    CurrentPageTitle = "설정";
                    break;
            }
        }
    }
}
