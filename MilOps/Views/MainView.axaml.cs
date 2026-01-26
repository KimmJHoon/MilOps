using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using MilOps.Services;
using MilOps.ViewModels;
using System;
using System.Threading.Tasks;

namespace MilOps.Views;

public partial class MainView : UserControl
{
    private readonly MainViewModel _viewModel;
    private TranslateTransform? _drawerTransform;
    private bool _isAnimating = false;

    public MainView()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        DataContext = _viewModel;

        _drawerTransform = DrawerPanel.RenderTransform as TranslateTransform;

        // View 이벤트 연결
        SetupCompanyRegisterView();
        SetupScheduleCreateView();
        SetupScheduleInputView();
        SetupScheduleReserveView();
        SetupScheduleConfirmView();
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
    public void OpenCompanyRegister()
    {
        _viewModel.OpenCompanyRegisterCommand.Execute(null);
    }

    /// <summary>
    /// 일정 생성 화면 열기 (외부에서 호출 가능)
    /// </summary>
    public void OpenScheduleCreate()
    {
        _viewModel.OpenScheduleCreateCommand.Execute(null);
    }

    /// <summary>
    /// 일정 상세 화면 열기 (역할과 상태에 따라 입력/예약/확정 화면으로 분기)
    /// </summary>
    public async void OpenScheduleInput(Guid scheduleId, string mode)
    {
        var currentUser = AuthService.CurrentUser;
        if (currentUser == null) return;

        System.Diagnostics.Debug.WriteLine($"[MainView] OpenScheduleInput - scheduleId: {scheduleId}, mode: {mode}, role: {currentUser.Role}");

        // 먼저 일정 상태 확인
        try
        {
            var client = SupabaseService.Client;
            if (client == null) return;

            var schedule = await client.From<MilOps.Models.Schedule>()
                .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, scheduleId.ToString())
                .Single();

            if (schedule == null)
            {
                System.Diagnostics.Debug.WriteLine($"[MainView] OpenScheduleInput - Schedule not found");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[MainView] OpenScheduleInput - Schedule status: {schedule.Status}");

            // 예약됨 상태 -> 확정 화면
            if (schedule.Status == "reserved")
            {
                OpenScheduleConfirm(scheduleId);
                return;
            }

            // 확정됨 상태 -> 확정 화면 (읽기 전용으로 보여줌)
            if (schedule.Status == "confirmed")
            {
                OpenScheduleConfirm(scheduleId);
                return;
            }

            // 역할에 따라 분기
            if (currentUser.Role == "user_local")
            {
                // 지자체담당자 -> 일정 입력 화면
                _viewModel.OpenScheduleInput(scheduleId);
                await ScheduleInputView.InitializeAsync(scheduleId, "input");
            }
            else if (currentUser.Role == "user_military")
            {
                // 대대담당자 -> 일정 예약 화면 (입력됨 상태일 때만)
                if (schedule.Status == "inputted")
                {
                    _viewModel.OpenScheduleReserve(scheduleId);
                    await ScheduleReserveView.InitializeAsync(scheduleId);
                }
                else
                {
                    // 생성됨 상태 등 -> 아직 지자체담당자가 입력 안 함
                    System.Diagnostics.Debug.WriteLine($"[MainView] OpenScheduleInput - Schedule not ready for reservation (status: {schedule.Status})");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[MainView] OpenScheduleInput - Unsupported role: {currentUser.Role}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainView] OpenScheduleInput - Error: {ex.Message}");
        }
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
    }

    /// <summary>
    /// 일정 확정 화면 열기 (예약된 일정에 대해 양측이 확정)
    /// </summary>
    public async void OpenScheduleConfirm(Guid scheduleId)
    {
        System.Diagnostics.Debug.WriteLine($"[MainView] OpenScheduleConfirm - scheduleId: {scheduleId}");
        _viewModel.OpenScheduleConfirm(scheduleId);
        await ScheduleConfirmView.InitializeAsync(scheduleId);
    }

    /// <summary>
    /// 로그인 후 사용자 역할 정보 갱신
    /// </summary>
    public void RefreshUserRole()
    {
        _viewModel.RefreshUserRole();
        System.Diagnostics.Debug.WriteLine($"[MainView] RefreshUserRole called - IsSuperAdmin: {_viewModel.IsSuperAdmin}");
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
            await AnimateTranslateX(_drawerTransform, -250, 0, duration);
        }
        else
        {
            // 동시에 애니메이션
            _ = AnimateOpacity(DrawerOverlay, 0.5, 0, duration);
            await AnimateTranslateX(_drawerTransform, 0, -250, duration);

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