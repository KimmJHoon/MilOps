using Avalonia.Controls;
using Avalonia.Interactivity;
using MilOps.Models;
using MilOps.Services;
using MilOps.ViewModels;
using System;

namespace MilOps.Views;

public partial class ManagerView : UserControl
{
    private ManagerViewModel? _viewModel;
    private Guid? _lastUserId;
    private string? _lastUserRole;

    // 채팅 시작 요청 이벤트 (MainView에서 구독)
    public event Action<ChatListItem>? OnChatStartRequested;

    public ManagerView()
    {
        InitializeComponent();

        // 로그아웃 전 정리 작업 등록
        AppRestartService.CleanupBeforeLogout += OnCleanupBeforeLogout;
    }

    private void OnCleanupBeforeLogout()
    {
        _viewModel?.Cleanup();
        _viewModel = null;
        _lastUserId = null;
        _lastUserRole = null;
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        InitializeViewModel();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        InitializeViewModel();
    }

    /// <summary>
    /// 강제 초기화 (탭 전환 시 호출)
    /// </summary>
    public void ForceInitialize()
    {
        InitializeViewModel();
    }

    private void InitializeViewModel()
    {
        // 현재 사용자 정보가 없으면 무시
        if (AuthService.CurrentUser == null) return;

        var currentUserId = AuthService.CurrentUser.Id;
        var currentUserRole = AuthService.CurrentUser.Role;

        // 사용자가 변경되었거나 역할이 변경되었거나 처음 로드하는 경우 ViewModel 재생성
        bool needsNewViewModel = _viewModel == null ||
                                  _lastUserId != currentUserId ||
                                  _lastUserRole != currentUserRole;

        if (needsNewViewModel)
        {
            // 기존 ViewModel 이벤트 해제 및 정리
            if (_viewModel != null)
            {
                _viewModel.ChatStartRequested -= OnViewModelChatStartRequested;
                _viewModel.Cleanup();
            }

            // 새 ViewModel 생성
            _viewModel = new ManagerViewModel();
            _viewModel.ChatStartRequested += OnViewModelChatStartRequested;
            DataContext = _viewModel;
            _lastUserId = currentUserId;
            _lastUserRole = currentUserRole;
        }
        else
        {
            // 동일 사용자면 데이터만 새로고침
            _viewModel?.RefreshCommand.Execute(null);
        }
    }

    private void OnViewModelChatStartRequested(ChatListItem partner)
    {
        OnChatStartRequested?.Invoke(partner);
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // View가 제거될 때 이벤트 해제 및 리소스 정리
        if (_viewModel != null)
        {
            _viewModel.ChatStartRequested -= OnViewModelChatStartRequested;
            _viewModel.Cleanup();
        }
    }
}
