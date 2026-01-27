using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MilOps.ViewModels;
using System;

namespace MilOps.Views;

public partial class CalendarView : UserControl
{
    private CalendarViewModel? _viewModel;

    public CalendarView()
    {
        InitializeComponent();
        _viewModel = new CalendarViewModel();
        DataContext = _viewModel;

        // 일정 선택 이벤트 연결
        _viewModel.OnScheduleSelected += OnScheduleSelected;
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // 화면이 로드될 때 일정 로드
        if (_viewModel != null)
        {
            await _viewModel.LoadSchedulesAsync();
        }
    }

    private void OnScheduleSelected(Guid scheduleId)
    {
        // MainView를 통해 일정 확정 화면 열기
        var mainView = this.FindAncestorOfType<MainView>();
        if (mainView?.DataContext is MainViewModel mainVm)
        {
            mainVm.OpenScheduleConfirm(scheduleId);
        }
    }

    /// <summary>
    /// 외부에서 캘린더 새로고침 호출용
    /// </summary>
    public async void RefreshCalendar()
    {
        if (_viewModel != null)
        {
            await _viewModel.RefreshAsync();
        }
    }
}
