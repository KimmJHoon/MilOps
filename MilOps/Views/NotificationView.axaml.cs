using Avalonia.Controls;
using MilOps.Services;
using MilOps.ViewModels;
using System;

namespace MilOps.Views;

public partial class NotificationView : UserControl
{
    private NotificationViewModel? _viewModel;
    private Guid? _lastUserId;
    private bool _isInitialized = false;

    public NotificationView()
    {
        InitializeComponent();

        _viewModel = new NotificationViewModel();
        DataContext = _viewModel;

        // 로그아웃 시 상태 초기화
        AppRestartService.CleanupBeforeLogout += OnCleanupBeforeLogout;
    }

    private void OnCleanupBeforeLogout()
    {
        _lastUserId = null;
        _isInitialized = false;
        _viewModel?.ClearCache();
    }

    /// <summary>
    /// 탭 진입 시 호출 (MainView에서 호출)
    /// </summary>
    public async void OnTabEntered()
    {
        await InitializeOrRefreshAsync();
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
            // 캐시 초기화 및 새로 로드
            _viewModel.ClearCache();
            _lastUserId = currentUserId;

            await _viewModel.LoadNotificationsAsync();
            _isInitialized = true;
        }
    }

    /// <summary>
    /// 알림 새로고침 (외부에서 호출 가능)
    /// </summary>
    public async void RefreshNotifications()
    {
        if (_viewModel != null)
        {
            await _viewModel.LoadNotificationsAsync();
        }
    }

    /// <summary>
    /// 읽지 않은 알림 개수 조회 (외부에서 호출 가능)
    /// </summary>
    public async void LoadUnreadCount()
    {
        if (_viewModel != null)
        {
            await _viewModel.LoadUnreadCountAsync();
        }
    }

    // 이벤트 노출 (MainView에서 구독용)
    public event EventHandler? CloseRequested
    {
        add => _viewModel!.CloseRequested += value;
        remove => _viewModel!.CloseRequested -= value;
    }

    public event Action<Guid>? OnScheduleSelected
    {
        add => _viewModel!.OnScheduleSelected += value;
        remove => _viewModel!.OnScheduleSelected -= value;
    }
}
