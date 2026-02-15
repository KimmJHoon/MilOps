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

    private async void OnScheduleNavigationRequested(ScheduleNavigationArgs args)
    {
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

    private void OnTabChanged(string tabName)
    {
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
        LogoutRequested?.Invoke();
    }

    private void SetupCompanyRegisterView()
    {
        CompanyRegisterView.CloseRequested += OnCompanyRegisterCloseRequested;
    }

    private void SetupScheduleCreateView()
    {
        ScheduleCreateView.CloseRequested += OnScheduleCreateCloseRequested;
        ScheduleCreateView.ScheduleCreated += OnScheduleCreated;
    }

    private void SetupScheduleInputView()
    {
        ScheduleInputView.CloseRequested += OnScheduleInputCloseRequested;
        ScheduleInputView.ScheduleStatusChanged += OnScheduleInputStatusChanged;
    }

    private void SetupScheduleReserveView()
    {
        ScheduleReserveView.CloseRequested += OnScheduleReserveCloseRequested;
        ScheduleReserveView.ScheduleStatusChanged += OnScheduleReserveStatusChanged;
    }

    private void SetupScheduleConfirmView()
    {
        ScheduleConfirmView.CloseRequested += OnScheduleConfirmCloseRequested;
        ScheduleConfirmView.ScheduleStatusChanged += OnScheduleConfirmStatusChanged;
    }

    private void SetupNotificationView()
    {
        NotificationView.CloseRequested += OnNotificationCloseRequested;
        NotificationView.OnScheduleSelected += OnNotificationScheduleSelected;
    }

    private void OnNotificationCloseRequested(object? sender, EventArgs e)
    {
        _viewModel.SelectTabCommand.Execute("calendar");
    }

    private void OnNotificationScheduleSelected(Guid scheduleId)
    {
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
        _viewModel.CloseScheduleCreateCommand.Execute(null);
        RefreshScheduleList();
    }

    public void RefreshScheduleList()
    {
        ScheduleListView.ViewModel?.RefreshCommand.Execute(null);
    }

    public async void OpenCompanyRegister()
    {
        await CompanyRegisterView.ForceInitializeAsync();
        _viewModel.OpenCompanyRegisterCommand.Execute(null);
    }

    public async void OpenScheduleCreate()
    {
        await ScheduleCreateView.ForceInitializeAsync();
        _viewModel.OpenScheduleCreateCommand.Execute(null);
    }

    public void OpenScheduleInput(Guid scheduleId, string mode)
    {
        _viewModel.RequestOpenScheduleDetail(scheduleId, mode);
    }

    private void OnScheduleInputCloseRequested(object? sender, EventArgs e)
    {
        _viewModel.CloseScheduleInputCommand.Execute(null);
    }

    private void OnScheduleInputStatusChanged(object? sender, MilOps.ViewModels.ScheduleStatusChangedEventArgs e)
    {
        ScheduleListView.ViewModel?.UpdateScheduleStatus(e.ScheduleId, e.NewStatus, e.NewStatusOrder);
    }

    private void OnScheduleReserveCloseRequested(object? sender, EventArgs e)
    {
        _viewModel.CloseScheduleReserveCommand.Execute(null);
    }

    private void OnScheduleReserveStatusChanged(object? sender, MilOps.ViewModels.ScheduleStatusChangedEventArgs e)
    {
        ScheduleListView.ViewModel?.UpdateScheduleStatus(e.ScheduleId, e.NewStatus, e.NewStatusOrder);
    }

    private void OnScheduleConfirmCloseRequested(object? sender, EventArgs e)
    {
        _viewModel.CloseScheduleConfirmCommand.Execute(null);
    }

    private void OnScheduleConfirmStatusChanged(object? sender, MilOps.ViewModels.ScheduleStatusChangedEventArgs e)
    {
        ScheduleListView.ViewModel?.UpdateScheduleStatus(e.ScheduleId, e.NewStatus, e.NewStatusOrder);
        CalendarView.RefreshCalendar();
    }

    public async void OpenScheduleConfirm(Guid scheduleId)
    {
        try
        {
            _viewModel.OpenScheduleConfirm(scheduleId);
            await ScheduleConfirmView.InitializeAsync(scheduleId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainView] OpenScheduleConfirm error: {ex.Message}");
        }
    }

    public async Task RefreshUserRoleAsync()
    {
        _viewModel.RefreshUserRole();
        await InitializeCurrentTabAsync();
    }

    public void RefreshUserRole()
    {
        _ = RefreshUserRoleAsync();
    }

    private async Task InitializeCurrentTabAsync()
    {
        if (_viewModel.IsCalendarSelected)
        {
            await CalendarView.OnTabEnteredAsync();
        }
        else if (_viewModel.IsScheduleSelected)
        {
            ScheduleListView.ForceInitialize();
        }
        else if (_viewModel.IsManagerSelected)
        {
            ManagerView.ForceInitialize();
        }
        else if (_viewModel.IsNotificationSelected)
        {
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

        if (e.PropertyName == nameof(MainViewModel.IsScheduleSelected) && _viewModel.IsScheduleSelected)
        {
            ScheduleListView.ForceInitialize();
        }

        if (e.PropertyName == nameof(MainViewModel.IsManagerSelected) && _viewModel.IsManagerSelected)
        {
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

            _ = AnimateOpacity(DrawerOverlay, 0, 0.5, duration);
            await AnimateTranslateX(_drawerTransform, -280, 0, duration);
        }
        else
        {
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
