using System;
using System.Threading.Tasks;
using MilOps.Server.Protocol;

namespace MilOps.Server;

public class MessageRouter
{
    private readonly ConnectionManager _connectionManager;
    private readonly SupabaseMessageStore _messageStore;
    private readonly FcmNotifier _fcmNotifier;

    public MessageRouter(
        ConnectionManager connectionManager,
        SupabaseMessageStore messageStore,
        FcmNotifier fcmNotifier)
    {
        _connectionManager = connectionManager;
        _messageStore = messageStore;
        _fcmNotifier = fcmNotifier;
    }

    public async Task RouteAsync(ClientSession session, ChatPacket packet)
    {
        switch (packet.Type)
        {
            case PacketType.AUTH:
                await HandleAuthAsync(session, packet);
                break;
            case PacketType.MSG_SEND:
                await HandleMessageSendAsync(session, packet);
                break;
            case PacketType.MSG_READ:
                await HandleMessageReadAsync(session, packet);
                break;
            case PacketType.HEARTBEAT:
                await HandleHeartbeatAsync(session);
                break;
            default:
                Console.WriteLine($"[Router] Unhandled packet type: {packet.Type}");
                break;
        }
    }

    private async Task HandleAuthAsync(ClientSession session, ChatPacket packet)
    {
        if (packet.SenderId == null || packet.SenderId == Guid.Empty)
        {
            await session.SendPacketAsync(new ChatPacket
            {
                Type = PacketType.ERROR,
                Error = "Invalid senderId for AUTH"
            });
            return;
        }

        session.Authenticate(packet.SenderId.Value);
        _connectionManager.TryAdd(packet.SenderId.Value, session);

        await session.SendPacketAsync(new ChatPacket
        {
            Type = PacketType.AUTH_ACK,
            Success = true,
            SenderId = packet.SenderId
        });

        Console.WriteLine($"[Router] User {packet.SenderId} authenticated. Online: {_connectionManager.OnlineCount}");
    }

    private async Task HandleMessageSendAsync(ClientSession session, ChatPacket packet)
    {
        if (!session.IsAuthenticated)
        {
            await session.SendPacketAsync(new ChatPacket
            {
                Type = PacketType.ERROR,
                Error = "Not authenticated"
            });
            return;
        }

        if (packet.ReceiverId == null || string.IsNullOrEmpty(packet.Content))
        {
            await session.SendPacketAsync(new ChatPacket
            {
                Type = PacketType.ERROR,
                Error = "receiverId and content are required"
            });
            return;
        }

        try
        {
            // 1. DB에 메시지 저장
            var messageId = await _messageStore.SaveMessageAsync(
                session.UserId, packet.ReceiverId.Value, packet.Content);
            var timestamp = DateTimeOffset.UtcNow;

            // 2. 발신자에게 MSG_ACK 응답
            await session.SendPacketAsync(new ChatPacket
            {
                Type = PacketType.MSG_ACK,
                MessageId = messageId,
                Timestamp = timestamp,
                Success = true
            });

            // 3. 수신자에게 전달
            var receiverSession = _connectionManager.GetSession(packet.ReceiverId.Value);
            if (receiverSession != null)
            {
                // 온라인 → MSG_RECV 전송
                await receiverSession.SendPacketAsync(new ChatPacket
                {
                    Type = PacketType.MSG_RECV,
                    MessageId = messageId,
                    SenderId = session.UserId,
                    Content = packet.Content,
                    Timestamp = timestamp
                });
            }

            // 4-a. FCM 푸시 — 오프라인일 때만
            if (receiverSession == null)
            {
                _ = Task.Run(async () =>
                {
                    var preview = packet.Content.Length > 50
                        ? packet.Content[..50] + "..." : packet.Content;
                    try
                    {
                        await _fcmNotifier.NotifyAsync(
                            packet.ReceiverId.Value,
                            session.UserId.ToString(),
                            preview);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Router] FCM notify error: {ex.Message}");
                    }
                });
            }

            // 4-b. 알림 탭 저장 — 항상 (트리거에서 chat_message 제외됨 → 무한루프 없음)
            _ = Task.Run(async () =>
            {
                var preview = packet.Content.Length > 50
                    ? packet.Content[..50] + "..." : packet.Content;
                try
                {
                    await _messageStore.SaveChatNotificationAsync(
                        packet.ReceiverId.Value, session.UserId, preview);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Router] Notification save error: {ex.Message}");
                }
            });

            Console.WriteLine($"[Router] MSG {session.UserId} → {packet.ReceiverId} ({messageId})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Router] MSG_SEND error: {ex.Message}");
            await session.SendPacketAsync(new ChatPacket
            {
                Type = PacketType.MSG_ACK,
                Success = false,
                Error = "Failed to save message"
            });
        }
    }

    private async Task HandleMessageReadAsync(ClientSession session, ChatPacket packet)
    {
        if (!session.IsAuthenticated || packet.ReceiverId == null) return;

        try
        {
            // packet.ReceiverId = partnerId (원래 메시지를 보낸 사람)
            // session.UserId = myId (읽는 사람)
            // → partnerId가 보낸 메시지 중 myId가 받은 것 → 읽음 처리
            await _messageStore.MarkAsReadAsync(packet.ReceiverId.Value, session.UserId);

            // 원 발신자(partnerId)가 온라인이면 읽음 확인 전달
            var senderSession = _connectionManager.GetSession(packet.ReceiverId.Value);
            if (senderSession != null)
            {
                await senderSession.SendPacketAsync(new ChatPacket
                {
                    Type = PacketType.MSG_READ_ACK,
                    SenderId = session.UserId  // 읽은 사람
                });
            }

            Console.WriteLine($"[Router] READ {session.UserId} read messages from {packet.ReceiverId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Router] MSG_READ error: {ex.Message}");
        }
    }

    private async Task HandleHeartbeatAsync(ClientSession session)
    {
        await session.SendPacketAsync(new ChatPacket
        {
            Type = PacketType.HEARTBEAT,
            Timestamp = DateTimeOffset.UtcNow
        });
    }
}
