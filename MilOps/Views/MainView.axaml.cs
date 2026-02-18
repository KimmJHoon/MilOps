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
        SetupHomeView();
        SetupCompanyRegisterView();
        SetupScheduleCreateView();
        SetupScheduleInputView();
        SetupScheduleReserveView();
        SetupScheduleConfirmView();
        SetupNotificationView();
        SetupChatViews();

        // 시스템 뒤로가기 버튼 핸들러 등록 (fallback for MoveTaskToBack)
        AppRestartService.OnBackPressed = HandleBackPressed;

        // 딥링크/푸시 알림 네비게이션 구독
        DeepLinkService.NavigationRequested += OnDeepLinkNavigationRequested;

        // Avalonia TopLevel.BackRequested 이벤트 구독 (Android 뒤로가기 버튼 처리)
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            topLevel.BackRequested += OnBackRequested;
        }
    }

    private void OnDetachedFromVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            topLevel.BackRequested -= OnBackRequested;
        }
    }

    private void OnBackRequested(object? sender, RoutedEventArgs e)
    {
        if (HandleBackPressed())
        {
            e.Handled = true;
        }
        // Handled=false이면 Avalonia가 기본 동작(앱 종료) 실행
        // → MainActivity의 BackPressedCallback에서 MoveTaskToBack 처리
    }

    /// <summary>
    /// 시스템 뒤로가기 버튼 처리 - 열린 오버레이를 닫음
    /// </summary>
    private bool HandleBackPressed()
    {
        // 비밀번호 변경 모달이 열려있으면 닫기
        if (_viewModel.IsPasswordChangeVisible)
        {
            _viewModel.HidePasswordChangeCommand.Execute(null);
            return true;
        }

        // 드로어가 열려있으면 닫기
        if (_viewModel.IsDrawerOpen)
        {
            _viewModel.CloseDrawerCommand.Execute(null);
            return true;
        }

        // 채팅방이 열려있으면 닫기
        if (_viewModel.IsChatRoomOpen)
        {
            _viewModel.CloseChatRoomCommand.Execute(null);
            ChatListView.OnTabEntered();
            return true;
        }

        // 일정 관련 오버레이 닫기
        if (_viewModel.IsScheduleInputOpen)
        {
            _viewModel.CloseScheduleInputCommand.Execute(null);
            return true;
        }
        if (_viewModel.IsScheduleReserveOpen)
        {
            _viewModel.CloseScheduleReserveCommand.Execute(null);
            return true;
        }
        if (_viewModel.IsScheduleConfirmOpen)
        {
            _viewModel.CloseScheduleConfirmCommand.Execute(null);
            return true;
        }
        if (_viewModel.IsScheduleCreateOpen)
        {
            _viewModel.CloseScheduleCreateCommand.Execute(null);
            return true;
        }

        // 업체 등록 오버레이 닫기
        if (_viewModel.IsCompanyRegisterOpen)
        {
            _viewModel.CloseCompanyRegisterCommand.Execute(null);
            return true;
        }

        // 열린 오버레이 없음 - 기본 동작(백그라운드)으로 위임
        return false;
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
            case "home":
                HomeView.OnTabEntered();
                break;
            case "calendar":
                CalendarView.OnTabEntered();
                break;
            case "chat":
                ChatListView.OnTabEntered();
                break;
            case "notification":
                NotificationView.OnTabEntered();
                break;
            case "schedule":
                var filter = _pendingStatusFilter;
                _pendingStatusFilter = null;
                ScheduleListView.ForceInitialize(filter);
                break;
            case "manager":
                ManagerView.ForceInitialize();
                break;
        }
    }

    private void OnLogoutCompleted()
    {
        LogoutRequested?.Invoke();
    }

    private void SetupHomeView()
    {
        HomeView.OnScheduleSelected += OnHomeScheduleSelected;
        HomeView.OnViewAllSchedules += OnHomeViewAllSchedules;
        HomeView.OnStatusFilterSelected += OnHomeStatusFilterSelected;
    }

    private void OnHomeScheduleSelected(Guid scheduleId)
    {
        OpenScheduleInput(scheduleId, "view");
    }

    private void OnHomeViewAllSchedules()
    {
        _viewModel.SelectTabCommand.Execute("schedule");
    }

    private string? _pendingStatusFilter;

    private void OnHomeStatusFilterSelected(string statusFilter)
    {
        _pendingStatusFilter = statusFilter;
        _viewModel.SelectTabCommand.Execute("schedule");
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
        NotificationView.OnChatSelected += OnNotificationChatSelected;
    }

    private void OnNotificationCloseRequested(object? sender, EventArgs e)
    {
        _viewModel.SelectTabCommand.Execute("calendar");
    }

    private void OnNotificationScheduleSelected(Guid scheduleId)
    {
        OpenScheduleInput(scheduleId, "view");
    }

    private void OnNotificationChatSelected(object? sender, EventArgs e)
    {
        // 채팅 탭으로 이동
        _viewModel.SelectTabCommand.Execute("chat");
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
        InvalidateAndRefreshCalendar();
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
        InvalidateAndRefreshCalendar();
    }

    private void OnScheduleReserveCloseRequested(object? sender, EventArgs e)
    {
        _viewModel.CloseScheduleReserveCommand.Execute(null);
    }

    private void OnScheduleReserveStatusChanged(object? sender, MilOps.ViewModels.ScheduleStatusChangedEventArgs e)
    {
        ScheduleListView.ViewModel?.UpdateScheduleStatus(e.ScheduleId, e.NewStatus, e.NewStatusOrder);
        InvalidateAndRefreshCalendar();
    }

    private void OnScheduleConfirmCloseRequested(object? sender, EventArgs e)
    {
        _viewModel.CloseScheduleConfirmCommand.Execute(null);
    }

    private void OnScheduleConfirmStatusChanged(object? sender, MilOps.ViewModels.ScheduleStatusChangedEventArgs e)
    {
        ScheduleListView.ViewModel?.UpdateScheduleStatus(e.ScheduleId, e.NewStatus, e.NewStatusOrder);
        InvalidateAndRefreshCalendar();
    }

    /// <summary>
    /// 캘린더 캐시 무효화 후 새로고침 (일정 상태 변경 시 공통 호출)
    /// </summary>
    private void InvalidateAndRefreshCalendar()
    {
        // 현재 월의 캐시를 무효화하여 다음 로드 시 서버에서 최신 데이터를 가져옴
        var now = DateTime.Now;
        CalendarDataService.InvalidateMonth(now.Year, now.Month);
        // 인접 월도 무효화 (일정이 이전/다음 달에 걸칠 수 있음)
        var prevMonth = now.AddMonths(-1);
        var nextMonth = now.AddMonths(1);
        CalendarDataService.InvalidateMonth(prevMonth.Year, prevMonth.Month);
        CalendarDataService.InvalidateMonth(nextMonth.Year, nextMonth.Month);

        CalendarView.RefreshCalendar();
    }

    private void SetupChatViews()
    {
        ChatListView.OnConversationSelected += OnConversationSelected;
        ChatRoomView.CloseRequested += OnChatRoomCloseRequested;
        ManagerView.OnChatStartRequested += OnManagerChatStartRequested;
    }

    private void OnManagerChatStartRequested(Models.ChatListItem partner)
    {
        _viewModel.OpenChatRoom();
        ChatRoomView.EnterRoom(partner);
    }

    private void OnConversationSelected(Models.ChatListItem partner)
    {
        _viewModel.OpenChatRoom();
        ChatRoomView.EnterRoom(partner);
    }

    private void OnChatRoomCloseRequested(object? sender, EventArgs e)
    {
        _viewModel.CloseChatRoomCommand.Execute(null);
        // 대화방 닫으면 목록 새로고침
        ChatListView.OnTabEntered();
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
        // LoadUserRegionAsync와 InitializeCurrentTabAsync를 병렬 실행
        // 둘 다 QueryHelper.GetOrgDataAsync()를 사용하므로 동일한 Preload 캐시를 공유
        var regionTask = _viewModel.RefreshUserRoleAndGetRegionTask();
        var tabTask = InitializeCurrentTabAsync();
        var notificationTask = _viewModel.LoadUnreadNotificationCountAsync();
        await Task.WhenAll(regionTask, tabTask, notificationTask);
    }

    public void RefreshUserRole()
    {
        _ = RefreshUserRoleAsync();
    }

    /// <summary>
    /// 딥링크/푸시 알림 네비게이션 처리
    /// Android에서 알림 탭 → DeepLinkService → 여기로 전달
    /// </summary>
    private void OnDeepLinkNavigationRequested(DeepLinkArgs args)
    {
        // UI 스레드에서 실행
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[MainView] DeepLink navigation: tab={args.TargetTab}, type={args.Type}");

                // 탭 전환
                _viewModel.SelectTabCommand.Execute(args.TargetTab);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainView] DeepLink navigation error: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// 대기 중인 딥링크 처리 (cold start 시 MainView 초기화 후 호출)
    /// </summary>
    public void ProcessPendingDeepLink()
    {
        var pending = DeepLinkService.ConsumePending();
        if (pending != null)
        {
            OnDeepLinkNavigationRequested(pending);
        }
    }

    private async Task InitializeCurrentTabAsync()
    {
        if (_viewModel.IsHomeSelected)
        {
            HomeView.OnTabEntered();
        }
        else if (_viewModel.IsCalendarSelected)
        {
            await CalendarView.OnTabEnteredAsync();
        }
        else if (_viewModel.IsScheduleSelected)
        {
            ScheduleListView.ForceInitialize();
        }
        else if (_viewModel.IsChatSelected)
        {
            ChatListView.OnTabEntered();
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

    private void OnOverlayTapped(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _viewModel.CloseDrawerCommand.Execute(null);
    }

    private async void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsDrawerOpen) && !_isAnimating)
        {
            await AnimateDrawer(_viewModel.IsDrawerOpen);
        }

        // 주의: ScheduleListView.ForceInitialize()와 ManagerView.ForceInitialize()는
        // OnTabChanged()에서 이미 호출되므로 여기서 중복 호출하지 않음.
        // SelectTab() → IsScheduleSelected = true (PropertyChanged) → 여기서 호출 1번
        // SelectTab() → TabChanged?.Invoke() → OnTabChanged() → 호출 2번
        // → 중복 호출로 네트워크 요청 2배, ViewModel 재생성 2배 발생했던 버그.
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
