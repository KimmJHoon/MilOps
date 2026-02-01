using Avalonia.Controls;
using MilOps.ViewModels;
using System;

namespace MilOps.Views;

public partial class NotificationView : UserControl
{
    private NotificationViewModel? _viewModel;

    public NotificationView()
    {
        InitializeComponent();

        _viewModel = new NotificationViewModel();
        DataContext = _viewModel;

        // Loaded 이벤트에서 알림 로드
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            await _viewModel.LoadNotificationsAsync();
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
