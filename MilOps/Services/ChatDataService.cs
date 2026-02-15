using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MilOps.Models;
using static Supabase.Postgrest.Constants;

namespace MilOps.Services;

/// <summary>
/// 채팅 데이터 서비스 — Supabase RPC + 로컬 캐시 통합
/// </summary>
public static class ChatDataService
{
    /// <summary>
    /// 대화 목록 조회 (get_conversations RPC)
    /// </summary>
    public static async Task<List<ChatListItem>> GetConversationsAsync(Guid userId)
    {
        try
        {
            var client = SupabaseService.Client;
            if (client == null) return new List<ChatListItem>();

            var result = await client.Rpc("get_conversations",
                new Dictionary<string, object> { { "p_user_id", userId.ToString() } });

            if (result?.Content == null) return new List<ChatListItem>();

            var items = Newtonsoft.Json.JsonConvert.DeserializeObject<List<ChatListItem>>(result.Content);
            return items ?? new List<ChatListItem>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChatDataService] GetConversationsAsync error: {ex.Message}");
            return new List<ChatListItem>();
        }
    }

    /// <summary>
    /// 대화 상대에게 보낸/받은 메시지 목록 조회 (최신 N건)
    /// 서버 조회 후 로컬 캐시에 저장, 오프라인 시 캐시 fallback
    /// </summary>
    public static async Task<List<Message>> GetMessagesAsync(Guid myId, Guid partnerId, int limit = 50)
    {
        try
        {
            var client = SupabaseService.Client;
            if (client == null) return new List<Message>();

            var sent = await client.From<Message>()
                .Filter("sender_id", Operator.Equals, myId.ToString())
                .Filter("receiver_id", Operator.Equals, partnerId.ToString())
                .Order("created_at", Ordering.Descending)
                .Limit(limit)
                .Get();

            var received = await client.From<Message>()
                .Filter("sender_id", Operator.Equals, partnerId.ToString())
                .Filter("receiver_id", Operator.Equals, myId.ToString())
                .Order("created_at", Ordering.Descending)
                .Limit(limit)
                .Get();

            var messages = new List<Message>();
            if (sent?.Models != null) messages.AddRange(sent.Models);
            if (received?.Models != null) messages.AddRange(received.Models);

            messages.Sort((a, b) => a.CreatedAt.CompareTo(b.CreatedAt));

            if (messages.Count > limit)
                messages = messages.GetRange(messages.Count - limit, limit);

            // 로컬 캐시에 저장
            await CacheMessagesAsync(messages);

            return messages;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChatDataService] GetMessagesAsync error: {ex.Message}");
            return await GetCachedMessagesAsync(partnerId.ToString(), limit);
        }
    }

    /// <summary>
    /// 낙관적 업데이트 시 메시지를 즉시 로컬 캐시에 저장
    /// 대화방 재진입 시 캐시에서 복원 가능
    /// </summary>
    public static async Task SaveMessageLocallyAsync(Guid messageId, Guid senderId, Guid receiverId, string content)
    {
        try
        {
            var local = new LocalChatMessage
            {
                Id = messageId.ToString(),
                SenderId = senderId.ToString(),
                ReceiverId = receiverId.ToString(),
                Content = content,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
            await LocalChatDatabase.SaveMessagesAsync(new[] { local });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChatDataService] SaveMessageLocallyAsync error: {ex.Message}");
        }
    }

    private static async Task CacheMessagesAsync(List<Message> messages)
    {
        try
        {
            var locals = new List<LocalChatMessage>();
            foreach (var m in messages)
            {
                locals.Add(new LocalChatMessage
                {
                    Id = m.Id.ToString(),
                    SenderId = m.SenderId.ToString(),
                    ReceiverId = m.ReceiverId.ToString(),
                    Content = m.Content,
                    IsRead = m.IsRead,
                    CreatedAt = m.CreatedAt.UtcDateTime
                });
            }
            await LocalChatDatabase.SaveMessagesAsync(locals);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChatDataService] CacheMessagesAsync error: {ex.Message}");
        }
    }

    private static async Task<List<Message>> GetCachedMessagesAsync(string partnerId, int limit)
    {
        try
        {
            var locals = await LocalChatDatabase.GetMessagesAsync(partnerId, limit);
            var messages = new List<Message>();
            foreach (var l in locals)
            {
                messages.Add(new Message
                {
                    Id = Guid.TryParse(l.Id, out var id) ? id : Guid.Empty,
                    SenderId = Guid.TryParse(l.SenderId, out var sid) ? sid : Guid.Empty,
                    ReceiverId = Guid.TryParse(l.ReceiverId, out var rid) ? rid : Guid.Empty,
                    Content = l.Content,
                    IsRead = l.IsRead,
                    CreatedAt = new DateTimeOffset(l.CreatedAt, TimeSpan.Zero)
                });
            }
            return messages;
        }
        catch
        {
            return new List<Message>();
        }
    }
}
