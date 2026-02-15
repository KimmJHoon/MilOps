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

/// <summary>
/// 메시지 버블 표시용 래퍼
/// </summary>
public class ChatBubble
{
    public string Content { get; set; } = "";
    public bool IsMine { get; set; }
    public bool IsRead { get; set; }
    public string TimeText { get; set; } = "";
    public Guid MessageId { get; set; }
    public bool IsServerConfirmed { get; set; } // 서버 ACK 수신 여부
}

public partial class ChatRoomViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<ChatBubble> _messages = new();

    [ObservableProperty]
    private string _messageInput = "";

    [ObservableProperty]
    private string _partnerName = "";

    [ObservableProperty]
    private string _partnerRoleDisplay = "";

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private bool _isConnected = false;

    private Guid _myId;
    private Guid _partnerId;

    public event Action? CloseRequested;
    public event Action? MessageSent; // 목록 새로고침용

    public ChatRoomViewModel()
    {
        ChatTcpService.MessageReceived += OnMessageReceived;
        ChatTcpService.MessageAcked += OnMessageAcked;
        ChatTcpService.ReadAcked += OnReadAcked;
        ChatTcpService.ConnectionStateChanged += OnConnectionStateChanged;

        IsConnected = ChatTcpService.IsConnected;
    }

    /// <summary>
    /// 대화방 진입 — 상대 정보 세팅 + 메시지 로드
    /// </summary>
    public async Task EnterRoomAsync(ChatListItem partner)
    {
        var user = AuthService.CurrentUser;
        if (user == null) return;

        _myId = user.Id;
        _partnerId = partner.PartnerId;
        PartnerName = partner.PartnerName;
        PartnerRoleDisplay = partner.PartnerRoleDisplayName;

        await LoadMessagesAsync();

        // 읽음 처리 전송
        await ChatTcpService.SendReadAsync(_myId, _partnerId);
    }

    private async Task LoadMessagesAsync()
    {
        IsLoading = true;

        try
        {
            var msgs = await ChatDataService.GetMessagesAsync(_myId, _partnerId, 50);

            Messages.Clear();
            foreach (var m in msgs)
            {
                Messages.Add(new ChatBubble
                {
                    MessageId = m.Id,
                    Content = m.Content,
                    IsMine = m.SenderId == _myId,
                    IsRead = m.IsRead,
                    TimeText = FormatTime(m.CreatedAt),
                    IsServerConfirmed = true // DB에서 로드된 메시지는 이미 확인됨
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChatRoomVM] LoadMessages error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Send()
    {
        var text = MessageInput?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        MessageInput = "";

        // 로컬 캐시용 임시 ID 생성
        var localId = Guid.NewGuid();

        // 즉시 UI에 표시 (낙관적 업데이트)
        Messages.Add(new ChatBubble
        {
            Content = text,
            IsMine = true,
            IsRead = false,
            TimeText = FormatTime(DateTimeOffset.UtcNow),
            MessageId = localId
        });

        MessageSent?.Invoke();

        // 로컬 캐시에 즉시 저장 + TCP 전송 (백그라운드, UI 블로킹 없음)
        var myId = _myId;
        var partnerId = _partnerId;
        _ = Task.Run(async () =>
        {
            try
            {
                // 로컬 캐시에 저장 (대화방 재진입 시 복원용)
                await ChatDataService.SaveMessageLocallyAsync(localId, myId, partnerId, text);

                // TCP 전송
                await ChatTcpService.SendMessageAsync(myId, partnerId, text);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ChatRoomVM] Send error: {ex.Message}");
            }
        });
    }

    [RelayCommand]
    private void GoBack()
    {
        CloseRequested?.Invoke();
    }

    // ===== TCP 이벤트 핸들러 =====

    private void OnMessageReceived(ChatPacket packet)
    {
        // 현재 대화 상대의 메시지만 처리
        if (packet.SenderId != _partnerId) return;

        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            Messages.Add(new ChatBubble
            {
                MessageId = packet.MessageId ?? Guid.Empty,
                Content = packet.Content ?? "",
                IsMine = false,
                IsRead = true,
                TimeText = FormatTime(packet.Timestamp ?? DateTimeOffset.UtcNow)
            });

            // 받은 메시지 읽음 처리
            await ChatTcpService.SendReadAsync(_myId, _partnerId);
        });
    }

    private void OnMessageAcked(ChatPacket packet)
    {
        // MSG_ACK: 서버가 전송을 확인 — 미확인 버블의 ID를 서버 ID로 교체
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (packet.MessageId.HasValue)
            {
                var bubble = Messages.FirstOrDefault(b => b.IsMine && !b.IsServerConfirmed);
                if (bubble != null)
                {
                    bubble.MessageId = packet.MessageId.Value;
                    bubble.IsServerConfirmed = true;
                }
            }
        });
    }

    private void OnReadAcked(ChatPacket packet)
    {
        // 상대가 읽었으면 내 메시지를 읽음 표시
        if (packet.SenderId != _partnerId) return;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            foreach (var bubble in Messages.Where(b => b.IsMine && !b.IsRead))
            {
                bubble.IsRead = true;
            }
            // ObservableCollection은 아이템 프로퍼티 변경을 자동 감지하지 않으므로
            // 필요 시 컬렉션을 갱신
            OnPropertyChanged(nameof(Messages));
        });
    }

    private void OnConnectionStateChanged(bool connected)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            IsConnected = connected;
        });
    }

    private static string FormatTime(DateTimeOffset dt)
    {
        var local = dt.ToLocalTime();
        var ampm = local.Hour < 12 ? "오전" : "오후";
        var hour = local.Hour % 12;
        if (hour == 0) hour = 12;
        return $"{ampm} {hour}:{local.Minute:D2}";
    }

    public void Cleanup()
    {
        ChatTcpService.MessageReceived -= OnMessageReceived;
        ChatTcpService.MessageAcked -= OnMessageAcked;
        ChatTcpService.ReadAcked -= OnReadAcked;
        ChatTcpService.ConnectionStateChanged -= OnConnectionStateChanged;
    }
}
