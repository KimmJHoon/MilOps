using Avalonia.Controls;
using MilOps.Models;
using MilOps.ViewModels;
using System;

namespace MilOps.Views;

public partial class ChatRoomView : UserControl
{
    private ChatRoomViewModel? _viewModel;

    public event EventHandler? CloseRequested;

    public ChatRoomView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 대화방 진입 — ChatListItem으로부터 상대 정보를 받아 초기화
    /// </summary>
    public async void EnterRoom(ChatListItem partner)
    {
        _viewModel = new ChatRoomViewModel();
        _viewModel.CloseRequested += OnCloseRequested;
        _viewModel.MessageSent += OnMessageSent;
        _viewModel.Messages.CollectionChanged += OnMessagesChanged;
        DataContext = _viewModel;

        await _viewModel.EnterRoomAsync(partner);
        ScrollToBottom();
    }

    private void OnCloseRequested()
    {
        _viewModel?.Cleanup();
        if (_viewModel != null)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
            _viewModel.MessageSent -= OnMessageSent;
            _viewModel.Messages.CollectionChanged -= OnMessagesChanged;
        }
        _viewModel = null;
        DataContext = null;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnMessageSent()
    {
        ScrollToBottom();

        // 메시지 전송 후 TextBox에 포커스 복원 (Android 소프트 키보드 유지)
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var inputBox = this.FindControl<TextBox>("MessageInputBox");
            inputBox?.Focus();
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void OnMessagesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
        {
            ScrollToBottom();
        }
    }

    private void ScrollToBottom()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var scroller = this.FindControl<ScrollViewer>("MessageScroller");
            scroller?.ScrollToEnd();
        }, Avalonia.Threading.DispatcherPriority.Background);
    }
}
