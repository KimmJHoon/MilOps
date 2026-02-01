using Avalonia.Controls;
using Avalonia.Interactivity;
using MilOps.Services;
using MilOps.ViewModels;
using System;

namespace MilOps.Views;

public partial class ManagerView : UserControl
{
    private ManagerViewModel? _viewModel;
    private Guid? _lastUserId;
    private string? _lastUserRole;

    public ManagerView()
    {
        InitializeComponent();

        // 로그아웃 전 정리 작업 등록
        AppRestartService.CleanupBeforeLogout += OnCleanupBeforeLogout;
    }

    private void OnCleanupBeforeLogout()
    {
        System.Diagnostics.Debug.WriteLine("[ManagerView] CleanupBeforeLogout - stopping timers and realtime");
        _viewModel?.Cleanup();
        _viewModel = null;
        _lastUserId = null;
        _lastUserRole = null;
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        System.Diagnostics.Debug.WriteLine($"[ManagerView] OnAttachedToVisualTree called");
        InitializeViewModel();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        System.Diagnostics.Debug.WriteLine($"[ManagerView] OnLoaded called");
        InitializeViewModel();
    }

    private void InitializeViewModel()
    {
        // 현재 사용자 정보가 없으면 무시
        if (AuthService.CurrentUser == null)
        {
            System.Diagnostics.Debug.WriteLine($"[ManagerView] InitializeViewModel - No current user, skipping");
            return;
        }

        var currentUserId = AuthService.CurrentUser.Id;
        var currentUserRole = AuthService.CurrentUser.Role;

        System.Diagnostics.Debug.WriteLine($"[ManagerView] InitializeViewModel - CurrentUser: {AuthService.CurrentUser.LoginId}, Role: {currentUserRole}, LastUserId: {_lastUserId}, LastRole: {_lastUserRole}");

        // 사용자가 변경되었거나 역할이 변경되었거나 처음 로드하는 경우 ViewModel 재생성
        bool needsNewViewModel = _viewModel == null ||
                                  _lastUserId != currentUserId ||
                                  _lastUserRole != currentUserRole;

        System.Diagnostics.Debug.WriteLine($"[ManagerView] NeedsNewViewModel: {needsNewViewModel} (vm={_viewModel != null}, userMatch={_lastUserId == currentUserId}, roleMatch={_lastUserRole == currentUserRole})");

        if (needsNewViewModel)
        {
            System.Diagnostics.Debug.WriteLine($"[ManagerView] >>> Creating new ViewModel for user: {AuthService.CurrentUser.LoginId}, role: {currentUserRole}");

            // 기존 ViewModel 정리
            _viewModel?.Cleanup();

            // 새 ViewModel 생성
            _viewModel = new ManagerViewModel();
            DataContext = _viewModel;
            _lastUserId = currentUserId;
            _lastUserRole = currentUserRole;

            System.Diagnostics.Debug.WriteLine($"[ManagerView] >>> ViewModel created and DataContext set");
        }
        else
        {
            // 동일 사용자면 데이터만 새로고침
            System.Diagnostics.Debug.WriteLine($"[ManagerView] Refreshing data for same user: {currentUserId}");
            _viewModel?.RefreshCommand.Execute(null);
        }
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // View가 제거될 때 타이머 정지 (리소스 절약)
        _viewModel?.Cleanup();
    }
}
