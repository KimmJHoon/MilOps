using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MilOps.Services;
using MilOps.ViewModels;
using System;
using System.Linq;

namespace MilOps.Views;

public partial class CalendarView : UserControl
{
    private CalendarViewModel? _viewModel;
    private Guid? _lastUserId;
    private bool _isInitialized = false;

    public CalendarView()
    {
        InitializeComponent();
        _viewModel = new CalendarViewModel();
        DataContext = _viewModel;

        // 일정 선택 이벤트 연결
        _viewModel.OnScheduleSelected += OnScheduleSelected;

        // 로그아웃 시 상태 초기화
        AppRestartService.CleanupBeforeLogout += OnCleanupBeforeLogout;
    }

    private void OnCleanupBeforeLogout()
    {
        _lastUserId = null;
        _isInitialized = false;
        _viewModel?.ClearCache();

        // CalendarDataService 캐시 초기화 (공유 캐시)
        CalendarDataService.ClearCache();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // OnLoaded에서는 초기화하지 않음 - MainView에서 TabChanged 이벤트로 처리
    }

    /// <summary>
    /// 탭 진입 시 호출 (MainView에서 호출) - async Task로 변경하여 완료까지 대기 가능
    /// </summary>
    public async System.Threading.Tasks.Task OnTabEnteredAsync()
    {
        try
        {
            await InitializeOrRefreshAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CalendarView] OnTabEnteredAsync error: {ex.Message}");
        }
    }

    /// <summary>
    /// 탭 진입 시 호출 (fire-and-forget용 - 기존 호환성 유지)
    /// </summary>
    public async void OnTabEntered()
    {
        await OnTabEnteredAsync();
    }

    private async System.Threading.Tasks.Task InitializeOrRefreshAsync()
    {
        if (_viewModel == null) return;

        var currentUserId = AuthService.CurrentUser?.Id;

        // 로그인하지 않은 상태면 무시
        if (currentUserId == null) return;

        // 사용자가 변경되었는지 확인
        bool userChanged = _lastUserId != currentUserId;

        if (userChanged || !_isInitialized)
        {
            // 캐시 초기화 (사용자 변경 시에만)
            if (userChanged)
            {
                _viewModel.ClearCache();
            }
            _lastUserId = currentUserId;

            // 필터 초기화 (백그라운드)
            await _viewModel.InitializeFiltersAsync();

            // 이미 Preload로 로딩 중이거나 데이터가 있으면 다시 로드하지 않음
            if (!CalendarDataService.IsLoading && _viewModel.Days.Count > 0 && _viewModel.Days.Any(d => d.IsCurrentMonth))
            {
                // Data already preloaded
            }
            else if (!CalendarDataService.IsLoading)
            {
                await _viewModel.LoadSchedulesAsync();
            }

            _isInitialized = true;
        }
    }

    private void OnScheduleSelected(Guid scheduleId)
    {
        // MainView를 통해 일정 확정 화면 열기
        var mainView = this.FindAncestorOfType<MainView>();
        if (mainView != null)
        {
            mainView.OpenScheduleConfirm(scheduleId);
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
