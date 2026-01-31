using Avalonia.Controls;
using MilOps.Models;
using MilOps.ViewModels;
using System;
using System.Threading.Tasks;

namespace MilOps.Views;

public partial class NotificationView : UserControl
{
    private NotificationViewModel? _viewModel;
    private bool _isRealtimeStarted = false;

    public NotificationView()
    {
        InitializeComponent();

        _viewModel = new NotificationViewModel();
        DataContext = _viewModel;

        // Loaded 이벤트에서 알림 로드 및 실시간 구독 시작
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            await _viewModel.LoadNotificationsAsync();

            // 실시간 구독 시작 (최초 1회만)
            if (!_isRealtimeStarted)
            {
                await _viewModel.StartRealtimeSubscriptionAsync();
                _isRealtimeStarted = true;
            }
        }
    }

    private void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // 뷰가 언로드되어도 실시간 구독은 유지
        // (앱 전체에서 알림을 계속 수신해야 함)
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

    /// <summary>
    /// 실시간 구독 시작 (외부에서 호출 가능)
    /// </summary>
    public async void StartRealtimeSubscription()
    {
        System.Diagnostics.Debug.WriteLine($"[NotificationView] StartRealtimeSubscription called, _isRealtimeStarted: {_isRealtimeStarted}");

        if (_viewModel != null && !_isRealtimeStarted)
        {
            try
            {
                await _viewModel.StartRealtimeSubscriptionAsync();
                _isRealtimeStarted = true;
                System.Diagnostics.Debug.WriteLine("[NotificationView] Realtime subscription started successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotificationView] StartRealtimeSubscription error: {ex.Message}");
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[NotificationView] Skipped - _viewModel null: {_viewModel == null}, already started: {_isRealtimeStarted}");
        }
    }

    /// <summary>
    /// 실시간 구독 중지 (로그아웃 시 호출)
    /// </summary>
    public void StopRealtimeSubscription()
    {
        if (_viewModel != null)
        {
            _viewModel.StopRealtimeSubscription();
            _isRealtimeStarted = false;
        }
    }

    /// <summary>
    /// 정리 (로그아웃 시 호출)
    /// </summary>
    public void Cleanup()
    {
        _viewModel?.Cleanup();
        _isRealtimeStarted = false;
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

    /// <summary>
    /// 새 알림 수신 이벤트 (토스트 알림 등에 사용)
    /// </summary>
    public event Action<Notification>? OnNewNotificationReceived
    {
        add => _viewModel!.OnNewNotificationReceived += value;
        remove => _viewModel!.OnNewNotificationReceived -= value;
    }
}
