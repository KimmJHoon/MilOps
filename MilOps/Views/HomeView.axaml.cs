using Avalonia.Controls;
using MilOps.Services;
using MilOps.ViewModels;
using System;

namespace MilOps.Views;

public partial class HomeView : UserControl
{
    private HomeViewModel? _viewModel;
    private Guid? _lastUserId;
    private DateTime _lastLoadTime = DateTime.MinValue;

    // 캐시 유효 시간: 30초 이내 재진입 시 서버 재조회 스킵
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    public HomeView()
    {
        InitializeComponent();

        _viewModel = new HomeViewModel();
        DataContext = _viewModel;

        // 로그아웃 시 상태 초기화
        AppRestartService.CleanupBeforeLogout += OnCleanupBeforeLogout;
    }

    private void OnCleanupBeforeLogout()
    {
        _lastUserId = null;
        _lastLoadTime = DateTime.MinValue;
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

        if (userChanged)
        {
            // 사용자 변경 시 캐시 초기화
            _viewModel.ClearCache();
            _lastUserId = currentUserId;
            _lastLoadTime = DateTime.MinValue;
        }

        // 캐시 유효 시간 내 재진입이면 서버 재조회 스킵 (빠른 탭 전환 최적화)
        if (!userChanged && (DateTime.UtcNow - _lastLoadTime) < CacheTtl)
        {
            System.Diagnostics.Debug.WriteLine("[HomeView] Cache hit — skip reload");
            return;
        }

        await _viewModel.LoadDashboardAsync();
        _lastLoadTime = DateTime.UtcNow;
    }

    // === 이벤트 노출 (MainView에서 구독용) ===

    public event Action<Guid>? OnScheduleSelected
    {
        add => _viewModel!.OnScheduleSelected += value;
        remove => _viewModel!.OnScheduleSelected -= value;
    }

    public event Action? OnViewAllSchedules
    {
        add => _viewModel!.OnViewAllSchedules += value;
        remove => _viewModel!.OnViewAllSchedules -= value;
    }

    public event Action<string>? OnStatusFilterSelected
    {
        add => _viewModel!.OnStatusFilterSelected += value;
        remove => _viewModel!.OnStatusFilterSelected -= value;
    }

}
