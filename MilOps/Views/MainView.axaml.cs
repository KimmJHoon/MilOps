using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
        ScheduleInputView.ScheduleUpdated += OnScheduleInputUpdated;
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
    /// 일정 입력/예약 화면 열기 (외부에서 호출 가능)
    /// </summary>
    public async void OpenScheduleInput(Guid scheduleId, string mode)
    {
        _viewModel.OpenScheduleInput(scheduleId, mode);
        await ScheduleInputView.InitializeAsync(scheduleId, mode);
    }

    private void OnScheduleInputCloseRequested(object? sender, EventArgs e)
    {
        _viewModel.CloseScheduleInputCommand.Execute(null);
    }

    private void OnScheduleInputUpdated(object? sender, EventArgs e)
    {
        // 일정 업데이트 완료 시 화면 닫기 및 목록 새로고침
        _viewModel.CloseScheduleInputCommand.Execute(null);
        RefreshScheduleList();
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