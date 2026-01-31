using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using MilOps.ViewModels;
using System;
using System.Threading.Tasks;

namespace MilOps.Views;

public partial class MainView : UserControl
{
    private readonly MainViewModel _viewModel;
    private TranslateTransform? _drawerTransform;
    private bool _isAnimating = false;

    // 로그아웃 완료 이벤트 (외부에서 구독)
    public event Action? LogoutRequested;

    public MainView()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.LogoutCompleted += OnLogoutCompleted;
        _viewModel.TabChanged += OnTabChanged;
        _viewModel.ScheduleNavigationRequested += OnScheduleNavigationRequested;
        DataContext = _viewModel;

        _drawerTransform = DrawerPanel.RenderTransform as TranslateTransform;

        // View 이벤트 연결
        SetupCompanyRegisterView();
        SetupScheduleCreateView();
        SetupScheduleInputView();
        SetupScheduleReserveView();
        SetupScheduleConfirmView();
        SetupNotificationView();
    }

    /// <summary>
    /// ViewModel에서 일정 화면 이동 요청 시 호출 (MVVM 패턴: View는 화면 전환만 담당)
    /// </summary>
    private async void OnScheduleNavigationRequested(ScheduleNavigationArgs args)
    {
        System.Diagnostics.Debug.WriteLine($"[MainView] OnScheduleNavigationRequested - Type: {args.NavigationType}, Id: {args.ScheduleId}");

        switch (args.NavigationType)
        {
            case ScheduleNavigationType.Input:
                _viewModel.OpenScheduleInput(args.ScheduleId);
                await ScheduleInputView.InitializeAsync(args.ScheduleId, "input");
                break;
            case ScheduleNavigationType.Reserve:
                _viewModel.OpenScheduleReserve(args.ScheduleId);
                await ScheduleReserveView.InitializeAsync(args.ScheduleId);
                break;
            case ScheduleNavigationType.Confirm:
                _viewModel.OpenScheduleConfirm(args.ScheduleId);
                await ScheduleConfirmView.InitializeAsync(args.ScheduleId);
                break;
        }
    }

    /// <summary>
    /// 탭 변경 시 호출 - 해당 View 초기화
    /// </summary>
    private void OnTabChanged(string tabName)
    {
        System.Diagnostics.Debug.WriteLine($"[MainView] OnTabChanged: {tabName}");

        switch (tabName)
        {
            case "calendar":
                CalendarView.OnTabEntered();
                break;
            case "notification":
                NotificationView.OnTabEntered();
                break;
            case "schedule":
                ScheduleListView.ForceInitialize();
                break;
        }
    }

    private void OnLogoutCompleted()
    {
        System.Diagnostics.Debug.WriteLine("[MainView] OnLogoutCompleted - invoking LogoutRequested");
        LogoutRequested?.Invoke();
    }

    private void SetupCompanyRegisterView()
    {
        // CompanyRegisterView의 CloseRequested 이벤트 구독
        CompanyRegisterView.CloseRequested += OnCompanyRegisterCloseRequested;
    }

    private void SetupScheduleCreateView()
    {
        // ScheduleCreateView의 CloseRequested 이벤트 구독
        ScheduleCreateView.CloseRequested += OnScheduleCreateCloseRequested;
        ScheduleCreateView.ScheduleCreated += OnScheduleCreated;
    }

    private void SetupScheduleInputView()
    {
        // ScheduleInputView의 이벤트 구독
        ScheduleInputView.CloseRequested += OnScheduleInputCloseRequested;
        ScheduleInputView.ScheduleStatusChanged += OnScheduleInputStatusChanged;
    }

    private void SetupScheduleReserveView()
    {
        // ScheduleReserveView의 이벤트 구독
        ScheduleReserveView.CloseRequested += OnScheduleReserveCloseRequested;
        ScheduleReserveView.ScheduleStatusChanged += OnScheduleReserveStatusChanged;
    }

    private void SetupScheduleConfirmView()
    {
        // ScheduleConfirmView의 이벤트 구독
        ScheduleConfirmView.CloseRequested += OnScheduleConfirmCloseRequested;
        ScheduleConfirmView.ScheduleStatusChanged += OnScheduleConfirmStatusChanged;
    }

    private void SetupNotificationView()
    {
        // NotificationView의 이벤트 구독
        NotificationView.CloseRequested += OnNotificationCloseRequested;
        NotificationView.OnScheduleSelected += OnNotificationScheduleSelected;
    }

    private void OnNotificationCloseRequested(object? sender, EventArgs e)
    {
        // 알림 화면에서 닫기 시 캘린더로 이동
        _viewModel.SelectTabCommand.Execute("calendar");
    }

    private void OnNotificationScheduleSelected(Guid scheduleId)
    {
        // 알림 클릭 시 일정 상세 화면으로 이동
        OpenScheduleInput(scheduleId, "view");
    }

    private void OnCompanyRegisterCloseRequested(object? sender, EventArgs e)
    {
        _viewModel.CloseCompanyRegisterCommand.Execute(null);
    }

    private void OnScheduleCreateCloseRequested(object? sender, EventArgs e)
    {
        _viewModel.CloseScheduleCreateCommand.Execute(null);
    }

    private void OnScheduleCreated(object? sender, EventArgs e)
    {
        // 일정 생성 완료 시 화면 닫기 및 목록 새로고침
        _viewModel.CloseScheduleCreateCommand.Execute(null);

        // ScheduleListView 새로고침
        RefreshScheduleList();
    }

    /// <summary>
    /// 일정 목록 새로고침
    /// </summary>
    public void RefreshScheduleList()
    {
        ScheduleListView.ViewModel?.RefreshCommand.Execute(null);
        System.Diagnostics.Debug.WriteLine("[MainView] RefreshScheduleList called");
    }

    /// <summary>
    /// 업체 등록 화면 열기 (외부에서 호출 가능)
    /// </summary>
    public async void OpenCompanyRegister()
    {
        // 먼저 ViewModel 초기화 (사용자 변경 시 재생성)
        await CompanyRegisterView.ForceInitializeAsync();
        _viewModel.OpenCompanyRegisterCommand.Execute(null);
    }

    /// <summary>
    /// 일정 생성 화면 열기 (외부에서 호출 가능)
    /// </summary>
    public async void OpenScheduleCreate()
    {
        // 먼저 ViewModel 초기화 (사용자 변경 시 재생성)
        await ScheduleCreateView.ForceInitializeAsync();
        _viewModel.OpenScheduleCreateCommand.Execute(null);
    }

    /// <summary>
    /// 일정 상세 화면 열기 (MVVM 패턴: ViewModel에 위임하여 DB 조회 및 화면 분기 처리)
    /// View에서는 직접 DB를 호출하지 않음
    /// </summary>
    public void OpenScheduleInput(Guid scheduleId, string mode)
    {
        System.Diagnostics.Debug.WriteLine($"[MainView] OpenScheduleInput - scheduleId: {scheduleId}, mode: {mode}");

        // ViewModel에 일정 상세 화면 열기 요청 (백그라운드에서 DB 조회 후 이벤트로 통지)
        _viewModel.RequestOpenScheduleDetail(scheduleId, mode);
    }

    private void OnScheduleInputCloseRequested(object? sender, EventArgs e)
    {
        _viewModel.CloseScheduleInputCommand.Execute(null);
    }

    private void OnScheduleInputStatusChanged(object? sender, MilOps.ViewModels.ScheduleStatusChangedEventArgs e)
    {
        // 입력 완료 시 해당 일정의 상태만 직접 업데이트 (리프레시 없이)
        System.Diagnostics.Debug.WriteLine($"[MainView] OnScheduleInputStatusChanged - id: {e.ScheduleId}, status: {e.NewStatus}");
        ScheduleListView.ViewModel?.UpdateScheduleStatus(e.ScheduleId, e.NewStatus, e.NewStatusOrder);
    }

    private void OnScheduleReserveCloseRequested(object? sender, EventArgs e)
    {
        _viewModel.CloseScheduleReserveCommand.Execute(null);
    }

    private void OnScheduleReserveStatusChanged(object? sender, MilOps.ViewModels.ScheduleStatusChangedEventArgs e)
    {
        // 예약 완료 시 해당 일정의 상태만 직접 업데이트 (리프레시 없이)
        System.Diagnostics.Debug.WriteLine($"[MainView] OnScheduleReserveStatusChanged - id: {e.ScheduleId}, status: {e.NewStatus}");
        ScheduleListView.ViewModel?.UpdateScheduleStatus(e.ScheduleId, e.NewStatus, e.NewStatusOrder);
    }

    private void OnScheduleConfirmCloseRequested(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[MainView] OnScheduleConfirmCloseRequested");
        _viewModel.CloseScheduleConfirmCommand.Execute(null);
    }

    private void OnScheduleConfirmStatusChanged(object? sender, MilOps.ViewModels.ScheduleStatusChangedEventArgs e)
    {
        // 확정 완료 시 해당 일정의 상태만 직접 업데이트 (리프레시 없이)
        System.Diagnostics.Debug.WriteLine($"[MainView] OnScheduleConfirmStatusChanged - id: {e.ScheduleId}, status: {e.NewStatus}");
        ScheduleListView.ViewModel?.UpdateScheduleStatus(e.ScheduleId, e.NewStatus, e.NewStatusOrder);

        // 캘린더도 새로고침 (확정 상태 반영)
        CalendarView.RefreshCalendar();
    }

    /// <summary>
    /// 일정 확정 화면 열기 (예약된 일정에 대해 양측이 확정)
    /// </summary>
    public async void OpenScheduleConfirm(Guid scheduleId)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[MainView] OpenScheduleConfirm - scheduleId: {scheduleId}");
            _viewModel.OpenScheduleConfirm(scheduleId);
            await ScheduleConfirmView.InitializeAsync(scheduleId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainView] OpenScheduleConfirm error: {ex.Message}");
        }
    }

    /// <summary>
    /// 로그인 후 사용자 역할 정보 갱신 - async Task로 변경하여 완료까지 대기 가능
    /// </summary>
    public async Task RefreshUserRoleAsync()
    {
        _viewModel.RefreshUserRole();
        System.Diagnostics.Debug.WriteLine($"[MainView] RefreshUserRoleAsync called - IsSuperAdmin: {_viewModel.IsSuperAdmin}");

        // 로그인 후 현재 선택된 탭의 View 강제 초기화 (완료까지 대기)
        await InitializeCurrentTabAsync();
        System.Diagnostics.Debug.WriteLine("[MainView] RefreshUserRoleAsync completed");
    }

    /// <summary>
    /// 로그인 후 사용자 역할 정보 갱신 (기존 호환성 유지)
    /// </summary>
    public void RefreshUserRole()
    {
        _ = RefreshUserRoleAsync();
    }

    /// <summary>
    /// 현재 선택된 탭의 View 초기화 (로그인 직후 호출) - async Task로 변경하여 완료까지 대기 가능
    /// </summary>
    private async Task InitializeCurrentTabAsync()
    {
        System.Diagnostics.Debug.WriteLine($"[MainView] InitializeCurrentTabAsync - Calendar: {_viewModel.IsCalendarSelected}, Schedule: {_viewModel.IsScheduleSelected}, Manager: {_viewModel.IsManagerSelected}");

        if (_viewModel.IsCalendarSelected)
        {
            System.Diagnostics.Debug.WriteLine("[MainView] InitializeCurrentTabAsync - Initializing CalendarView");
            await CalendarView.OnTabEnteredAsync();
            System.Diagnostics.Debug.WriteLine("[MainView] InitializeCurrentTabAsync - CalendarView initialized");
        }
        else if (_viewModel.IsScheduleSelected)
        {
            System.Diagnostics.Debug.WriteLine("[MainView] InitializeCurrentTabAsync - Initializing ScheduleListView");
            ScheduleListView.ForceInitialize();
        }
        else if (_viewModel.IsManagerSelected)
        {
            System.Diagnostics.Debug.WriteLine("[MainView] InitializeCurrentTabAsync - Initializing ManagerView");
            ManagerView.ForceInitialize();
        }
        else if (_viewModel.IsNotificationSelected)
        {
            System.Diagnostics.Debug.WriteLine("[MainView] InitializeCurrentTabAsync - Initializing NotificationView");
            NotificationView.OnTabEntered();
        }
    }

    private void OnOverlayPressed(object? sender, PointerPressedEventArgs e)
    {
        _viewModel.CloseDrawerCommand.Execute(null);
    }

    private async void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsDrawerOpen) && !_isAnimating)
        {
            await AnimateDrawer(_viewModel.IsDrawerOpen);
        }

        // 탭 전환 시 해당 View 초기화 (사용자 변경 대응)
        if (e.PropertyName == nameof(MainViewModel.IsScheduleSelected) && _viewModel.IsScheduleSelected)
        {
            System.Diagnostics.Debug.WriteLine("[MainView] IsScheduleSelected changed to True - initializing ScheduleListView");
            ScheduleListView.ForceInitialize();
        }

        if (e.PropertyName == nameof(MainViewModel.IsManagerSelected) && _viewModel.IsManagerSelected)
        {
            System.Diagnostics.Debug.WriteLine("[MainView] IsManagerSelected changed to True - initializing ManagerView");
            ManagerView.ForceInitialize();
        }
    }

    private async Task AnimateDrawer(bool open)
    {
        if (_drawerTransform == null) return;

        _isAnimating = true;
        var duration = TimeSpan.FromMilliseconds(200);

        if (open)
        {
            DrawerOverlay.IsVisible = true;
            DrawerPanel.IsVisible = true;

            // 동시에 애니메이션
            _ = AnimateOpacity(DrawerOverlay, 0, 0.5, duration);
            await AnimateTranslateX(_drawerTransform, -280, 0, duration);
        }
        else
        {
            // 동시에 애니메이션
            _ = AnimateOpacity(DrawerOverlay, 0.5, 0, duration);
            await AnimateTranslateX(_drawerTransform, 0, -280, duration);

            DrawerOverlay.IsVisible = false;
            DrawerPanel.IsVisible = false;
        }

        _isAnimating = false;
    }

    private async Task AnimateOpacity(Control control, double from, double to, TimeSpan duration)
    {
        var steps = 20;
        var stepDuration = duration.TotalMilliseconds / steps;
        var diff = to - from;

        for (int i = 0; i <= steps; i++)
        {
            var progress = (double)i / steps;
            var easedProgress = 1 - Math.Pow(1 - progress, 3);
            control.Opacity = from + (diff * easedProgress);
            await Task.Delay((int)stepDuration);
        }

        control.Opacity = to;
    }

    private async Task AnimateTranslateX(TranslateTransform transform, double from, double to, TimeSpan duration)
    {
        var steps = 20;
        var stepDuration = duration.TotalMilliseconds / steps;
        var diff = to - from;

        for (int i = 0; i <= steps; i++)
        {
            var progress = (double)i / steps;
            var easedProgress = 1 - Math.Pow(1 - progress, 3);
            transform.X = from + (diff * easedProgress);
            await Task.Delay((int)stepDuration);
        }

        transform.X = to;
    }
}