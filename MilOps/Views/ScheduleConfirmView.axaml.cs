using Avalonia.Controls;
using Avalonia.Input;
using MilOps.ViewModels;
using System;
using System.Threading.Tasks;

namespace MilOps.Views;

public partial class ScheduleConfirmView : UserControl
{
    private ScheduleConfirmViewModel? _viewModel;

    // 이벤트: 화면 닫기 요청
    public event EventHandler? CloseRequested;

    // 이벤트: 일정 상태 변경됨 (scheduleId, newStatus, newStatusOrder 전달)
    public event EventHandler<ScheduleStatusChangedEventArgs>? ScheduleStatusChanged;

    public ScheduleConfirmView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 일정 ID로 초기화
    /// </summary>
    public async Task InitializeAsync(Guid scheduleId)
    {
        // 기존 ViewModel 정리
        if (_viewModel != null)
        {
            _viewModel.CloseRequested -= OnViewModelCloseRequested;
            _viewModel.ScheduleStatusChanged -= OnViewModelScheduleStatusChanged;
        }

        // 새 ViewModel 생성
        _viewModel = new ScheduleConfirmViewModel();
        _viewModel.CloseRequested += OnViewModelCloseRequested;
        _viewModel.ScheduleStatusChanged += OnViewModelScheduleStatusChanged;
        DataContext = _viewModel;

        await _viewModel.InitializeAsync(scheduleId);
    }

    private void OnViewModelCloseRequested(object? sender, EventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnViewModelScheduleStatusChanged(object? sender, ScheduleStatusChangedEventArgs e)
    {
        ScheduleStatusChanged?.Invoke(this, e);
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_viewModel != null)
        {
            _viewModel.CloseRequested -= OnViewModelCloseRequested;
            _viewModel.ScheduleStatusChanged -= OnViewModelScheduleStatusChanged;
        }
    }

    /// <summary>
    /// 모달 배경 오버레이 클릭 시 모달 닫기
    /// </summary>
    private void OnModalOverlayPressed(object? sender, PointerPressedEventArgs e)
    {
        _viewModel?.CancelConfirmDialogCommand.Execute(null);
    }
}
