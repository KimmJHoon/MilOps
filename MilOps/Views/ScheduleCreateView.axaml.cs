using Avalonia.Controls;
using MilOps.ViewModels;
using System;
using System.Threading.Tasks;

namespace MilOps.Views;

public partial class ScheduleCreateView : UserControl
{
    private ScheduleCreateViewModel? _viewModel;

    // 이벤트: 화면 닫기 요청 (부모에서 구독)
    public event EventHandler? CloseRequested;

    // 이벤트: 일정 생성 완료 (부모에서 구독하여 목록 새로고침 등)
    public event EventHandler? ScheduleCreated;

    public ScheduleCreateView()
    {
        InitializeComponent();
    }

    protected override async void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        System.Diagnostics.Debug.WriteLine("[ScheduleCreateView] OnAttachedToVisualTree");
        await InitializeViewModelAsync();
    }

    private async Task InitializeViewModelAsync()
    {
        System.Diagnostics.Debug.WriteLine("[ScheduleCreateView] InitializeViewModelAsync - START");

        // 기존 ViewModel이 있으면 이벤트 해제
        if (_viewModel != null)
        {
            _viewModel.CloseRequested -= OnViewModelCloseRequested;
            _viewModel.ScheduleCreated -= OnViewModelScheduleCreated;
        }

        // 항상 새로운 ViewModel 생성 (화면이 열릴 때마다 새로 시작)
        _viewModel = new ScheduleCreateViewModel();
        _viewModel.CloseRequested += OnViewModelCloseRequested;
        _viewModel.ScheduleCreated += OnViewModelScheduleCreated;
        DataContext = _viewModel;
        System.Diagnostics.Debug.WriteLine("[ScheduleCreateView] ViewModel created and events subscribed");

        await _viewModel.InitializeAsync();
        System.Diagnostics.Debug.WriteLine("[ScheduleCreateView] ViewModel initialized");
    }

    private void OnViewModelCloseRequested(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[ScheduleCreateView] CloseRequested from ViewModel");
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnViewModelScheduleCreated(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[ScheduleCreateView] ScheduleCreated from ViewModel");
        ScheduleCreated?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        System.Diagnostics.Debug.WriteLine("[ScheduleCreateView] OnDetachedFromVisualTree");

        if (_viewModel != null)
        {
            _viewModel.CloseRequested -= OnViewModelCloseRequested;
            _viewModel.ScheduleCreated -= OnViewModelScheduleCreated;
        }
    }
}
