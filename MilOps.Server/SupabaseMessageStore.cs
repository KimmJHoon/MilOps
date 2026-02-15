using System;
using System.Threading.Tasks;
using MilOps.Server.Models;
using static Supabase.Postgrest.Constants;

namespace MilOps.Server;

public class SupabaseMessageStore
{
    private readonly string _supabaseUrl;
    private readonly string _supabaseKey;
    private Supabase.Client? _client;

    public SupabaseMessageStore(string supabaseUrl, string supabaseKey)
    {
        _supabaseUrl = supabaseUrl;
        _supabaseKey = supabaseKey;
    }

    public async Task InitializeAsync()
    {
        _client = new Supabase.Client(_supabaseUrl, _supabaseKey);
        await _client.InitializeAsync();
        Console.WriteLine("[MessageStore] Supabase initialized");
    }

    public async Task<Guid> SaveMessageAsync(Guid senderId, Guid receiverId, string content)
    {
        if (_client == null)
            throw new InvalidOperationException("Supabase not initialized");

        var msg = new ServerMessage
        {
            Id = Guid.NewGuid(),
            SenderId = senderId,
            ReceiverId = receiverId,
            Content = content,
            IsRead = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _client.From<ServerMessage>().Insert(msg);
        return msg.Id;
    }

    public async Task MarkAsReadAsync(Guid senderId, Guid receiverId)
    {
        if (_client == null) return;

        // senderId가 보낸 메시지 중 receiverId가 받은 미읽음 메시지 → is_read = true
        await _client.From<ServerMessage>()
            .Filter("sender_id", Operator.Equals, senderId.ToString())
            .Filter("receiver_id", Operator.Equals, receiverId.ToString())
            .Filter("is_read", Operator.Equals, "false")
            .Set(x => x.IsRead, true)
            .Update();
    }

    /// <summary>
    /// 채팅 메시지 알림을 notifications 테이블에 저장 (앱 내 알림 탭 표시용)
    /// </summary>
    public async Task SaveChatNotificationAsync(Guid receiverId, Guid senderId, string messagePreview)
    {
        if (_client == null) return;

        try
        {
            // 발신자 이름 조회
            var senderName = "알 수 없음";
            try
            {
                var senderUser = await _client.From<ServerUser>()
                    .Filter("id", Operator.Equals, senderId.ToString())
                    .Single();

                if (senderUser != null && !string.IsNullOrEmpty(senderUser.Name))
                {
                    senderName = senderUser.Name;
                }
            }
            catch { /* 이름 조회 실패 시 기본값 사용 */ }

            var notification = new ServerNotification
            {
                Id = Guid.NewGuid(),
                UserId = receiverId,
                Type = "chat_message",
                Title = $"{senderName}님의 메시지",
                Message = messagePreview,
                IsRead = false,
                CreatedAt = DateTimeOffset.UtcNow
            };

            await _client.From<ServerNotification>().Insert(notification);
            Console.WriteLine($"[MessageStore] Chat notification saved for user {receiverId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MessageStore] SaveChatNotification error: {ex.Message}");
        }
    }
}
