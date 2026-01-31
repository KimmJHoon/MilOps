using Avalonia.Controls;
using Avalonia.Interactivity;
using MilOps.ViewModels;
using System;
using System.Threading.Tasks;

namespace MilOps.Views;

public partial class CompanyRegisterView : UserControl
{
    private CompanyRegisterViewModel? _viewModel;
    private Guid? _lastUserId;  // 마지막 사용자 ID 추적

    // 닫기 요청 이벤트 (외부에서 구독 가능)
    public event EventHandler? CloseRequested;

    public CompanyRegisterView()
    {
        InitializeComponent();
    }

    protected override async void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        System.Diagnostics.Debug.WriteLine("[CompanyRegisterView] OnAttachedToVisualTree called");
        await InitializeViewModelAsync();
    }

    /// <summary>
    /// 외부에서 호출 가능한 새로고침 메서드
    /// </summary>
    public async Task RefreshAsync()
    {
        System.Diagnostics.Debug.WriteLine("[CompanyRegisterView] RefreshAsync called");
        await InitializeViewModelAsync();
    }

    /// <summary>
    /// 강제 초기화 (화면 열 때 호출)
    /// </summary>
    public async Task ForceInitializeAsync()
    {
        System.Diagnostics.Debug.WriteLine("[CompanyRegisterView] ForceInitializeAsync called");
        await InitializeViewModelAsync();
    }

    private async Task InitializeViewModelAsync()
    {
        System.Diagnostics.Debug.WriteLine("[CompanyRegisterView] InitializeViewModelAsync called");

        var currentUser = MilOps.Services.AuthService.CurrentUser;
        var currentUserId = currentUser?.Id;

        // 사용자가 변경되었거나 ViewModel이 없으면 재생성
        bool needsNewViewModel = _viewModel == null || _lastUserId != currentUserId;

        if (needsNewViewModel)
        {
            System.Diagnostics.Debug.WriteLine($"[CompanyRegisterView] Creating new ViewModel - User changed: {_lastUserId} -> {currentUserId}");

            // 기존 ViewModel 정리
            if (_viewModel != null)
            {
                _viewModel.CloseRequested -= OnViewModelCloseRequested;
            }

            _viewModel = new CompanyRegisterViewModel();
            _viewModel.CloseRequested += OnViewModelCloseRequested;
            DataContext = _viewModel;
            _lastUserId = currentUserId;
        }

        await _viewModel.InitializeAsync();
    }

    private void OnViewModelCloseRequested(object? sender, EventArgs e)
    {
        // 외부로 이벤트 전파
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // 이벤트 해제
        if (_viewModel != null)
        {
            _viewModel.CloseRequested -= OnViewModelCloseRequested;
            _viewModel = null;
        }
    }
}
