using Avalonia.Controls;
using MilOps.Services;
using MilOps.ViewModels;
using System;

namespace MilOps.Views;

public partial class ManagerView : UserControl
{
    private ManagerViewModel? _viewModel;
    private Guid? _lastUserId;

    public ManagerView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);

        var currentUserId = AuthService.CurrentUser?.Id;

        // 사용자가 변경되었거나 처음 로드하는 경우 ViewModel 재생성
        if (_viewModel == null || _lastUserId != currentUserId)
        {
            System.Diagnostics.Debug.WriteLine($"[ManagerView] Creating new ViewModel for user: {currentUserId}");

            // 기존 ViewModel 정리
            _viewModel?.Cleanup();

            // 새 ViewModel 생성
            _viewModel = new ManagerViewModel();
            DataContext = _viewModel;
            _lastUserId = currentUserId;
        }
        else
        {
            // 동일 사용자면 데이터만 새로고침
            System.Diagnostics.Debug.WriteLine($"[ManagerView] Refreshing data for same user: {currentUserId}");
            _viewModel.RefreshCommand.Execute(null);
        }
    }

    protected override void OnUnloaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        // View가 언로드될 때 타이머 정지 (리소스 절약)
        _viewModel?.Cleanup();
    }
}
