using Avalonia.Controls;
using Avalonia.Interactivity;
using MilOps.ViewModels;
using System;
using System.Threading.Tasks;

namespace MilOps.Views;

public partial class CompanyRegisterView : UserControl
{
    private CompanyRegisterViewModel? _viewModel;

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

    private async Task InitializeViewModelAsync()
    {
        System.Diagnostics.Debug.WriteLine("[CompanyRegisterView] InitializeViewModelAsync called");

        if (_viewModel == null)
        {
            System.Diagnostics.Debug.WriteLine("[CompanyRegisterView] Creating new ViewModel");
            _viewModel = new CompanyRegisterViewModel();
            _viewModel.CloseRequested += OnViewModelCloseRequested;
            DataContext = _viewModel;
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
