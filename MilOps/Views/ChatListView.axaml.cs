using Avalonia.Controls;
using MilOps.Models;
using MilOps.Services;
using MilOps.ViewModels;
using System;

namespace MilOps.Views;

public partial class ChatListView : UserControl
{
    private ChatListViewModel? _viewModel;
    private Guid? _lastUserId;
    private bool _isInitialized = false;

    public ChatListView()
    {
        InitializeComponent();

        _viewModel = new ChatListViewModel();
        DataContext = _viewModel;

        AppRestartService.CleanupBeforeLogout += OnCleanupBeforeLogout;
    }

    private async void OnCleanupBeforeLogout()
    {
        _lastUserId = null;
        _isInitialized = false;
        _viewModel?.ClearCache();
        ChatTcpService.Disconnect();

        try { await LocalChatDatabase.ClearCacheAsync(); }
        catch { }
    }

    /// <summary>
    /// 탭 진입 시 호출 (MainView에서 호출)
    /// </summary>
    public async void OnTabEntered()
    {
        if (_viewModel == null) return;

        var currentUserId = AuthService.CurrentUser?.Id;
        if (currentUserId == null) return;

        bool userChanged = _lastUserId != currentUserId;

        if (userChanged || !_isInitialized)
        {
            _viewModel.ClearCache();
            _lastUserId = currentUserId;

            // TCP 연결 시도
            await _viewModel.ConnectAsync();
            _isInitialized = true;
        }

        // 항상 대화 목록 새로고침 (뱃지 갱신을 위해)
        await _viewModel.LoadConversationsAsync();
    }

    // 이벤트 노출 (MainView에서 구독용)
    public event Action<ChatListItem>? OnConversationSelected
    {
        add => _viewModel!.OnConversationSelected += value;
        remove => _viewModel!.OnConversationSelected -= value;
    }
}
