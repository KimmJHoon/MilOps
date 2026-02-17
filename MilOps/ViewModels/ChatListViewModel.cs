using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MilOps.Models;
using MilOps.Services;
using MilOps.Services.Protocol;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace MilOps.ViewModels;

public partial class ChatListViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<ChatListItem> _conversations = new();

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private bool _hasConversations = false;

    [ObservableProperty]
    private bool _isConnected = false;

    [ObservableProperty]
    private string _emptyMessage = "대화가 없습니다";

    // 대화 클릭 이벤트
    public event Action<ChatListItem>? OnConversationSelected;

    public ChatListViewModel()
    {
        ChatTcpService.MessageReceived += OnMessageReceived;
        ChatTcpService.ConnectionStateChanged += OnConnectionStateChanged;
    }

    public void ClearCache()
    {
        Conversations.Clear();
        HasConversations = false;
    }

    public async Task LoadConversationsAsync()
    {
        var user = AuthService.CurrentUser;
        if (user == null) return;

        IsLoading = true;

        try
        {
            var items = await ChatDataService.GetConversationsAsync(user.Id);

            // 일괄 교체 — N번 CollectionChanged → 1번
            Conversations = new ObservableCollection<ChatListItem>(items);

            HasConversations = Conversations.Any();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChatListVM] LoadConversationsAsync error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ConnectAsync()
    {
        var user = AuthService.CurrentUser;
        if (user == null) return;

        try
        {
            await ChatTcpService.ConnectAsync(user.Id);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChatListVM] ConnectAsync error: {ex.Message}");
        }
    }

    [RelayCommand]
    private void SelectConversation(ChatListItem? item)
    {
        if (item == null) return;
        OnConversationSelected?.Invoke(item);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadConversationsAsync();
    }

    private void OnMessageReceived(ChatPacket packet)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            await LoadConversationsAsync();
        });
    }

    private void OnConnectionStateChanged(bool connected)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            IsConnected = connected;
        });
    }

    public void Cleanup()
    {
        ChatTcpService.MessageReceived -= OnMessageReceived;
        ChatTcpService.ConnectionStateChanged -= OnConnectionStateChanged;
    }
}
