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
        await InitializeViewModelAsync();
    }

    /// <summary>
    /// 강제 초기화 (화면 열 때 호출)
    /// </summary>
    public async Task ForceInitializeAsync()
    {
        await InitializeViewModelAsync();
    }

    private async Task InitializeViewModelAsync()
    {
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

        await _viewModel.InitializeAsync();
    }

    private void OnViewModelCloseRequested(object? sender, EventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnViewModelScheduleCreated(object? sender, EventArgs e)
    {
        ScheduleCreated?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_viewModel != null)
        {
            _viewModel.CloseRequested -= OnViewModelCloseRequested;
            _viewModel.ScheduleCreated -= OnViewModelScheduleCreated;
        }
    }
    protected void OnCloseRequested()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    protected void OnScheduleCreated()
    {
        ScheduleCreated?.Invoke(this, EventArgs.Empty);
    }
}
