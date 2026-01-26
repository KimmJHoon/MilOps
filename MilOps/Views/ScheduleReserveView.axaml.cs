using Avalonia.Controls;
using MilOps.ViewModels;
using System;
using System.Threading.Tasks;

namespace MilOps.Views;

public partial class ScheduleReserveView : UserControl
{
    private ScheduleReserveViewModel? _viewModel;

    // 이벤트: 화면 닫기 요청
    public event EventHandler? CloseRequested;

    // 이벤트: 일정 업데이트 완료
    public event EventHandler? ScheduleUpdated;

    public ScheduleReserveView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 일정 ID로 초기화
    /// </summary>
    public async Task InitializeAsync(Guid scheduleId)
    {
        System.Diagnostics.Debug.WriteLine($"[ScheduleReserveView] InitializeAsync - scheduleId: {scheduleId}");

        // 기존 ViewModel 정리
        if (_viewModel != null)
        {
            _viewModel.CloseRequested -= OnViewModelCloseRequested;
            _viewModel.ScheduleUpdated -= OnViewModelScheduleUpdated;
        }

        // 새 ViewModel 생성
        _viewModel = new ScheduleReserveViewModel();
        _viewModel.CloseRequested += OnViewModelCloseRequested;
        _viewModel.ScheduleUpdated += OnViewModelScheduleUpdated;
        DataContext = _viewModel;

        await _viewModel.InitializeAsync(scheduleId);
    }

    private void OnViewModelCloseRequested(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[ScheduleReserveView] CloseRequested from ViewModel");
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnViewModelScheduleUpdated(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[ScheduleReserveView] ScheduleUpdated from ViewModel");
        ScheduleUpdated?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_viewModel != null)
        {
            _viewModel.CloseRequested -= OnViewModelCloseRequested;
            _viewModel.ScheduleUpdated -= OnViewModelScheduleUpdated;
        }
    }
}
