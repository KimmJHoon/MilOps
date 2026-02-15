using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace MilOps.Server;

/// <summary>
/// FCM push notification for offline users — Supabase Edge Function 호출
/// </summary>
public class FcmNotifier
{
    private readonly string _supabaseUrl;
    private readonly string _supabaseKey;
    private readonly HttpClient _httpClient = new();

    public FcmNotifier(string supabaseUrl, string supabaseKey)
    {
        _supabaseUrl = supabaseUrl;
        _supabaseKey = supabaseKey;
    }

    public async Task NotifyAsync(Guid userId, string senderName, string messagePreview)
    {
        try
        {
            var url = $"{_supabaseUrl}/functions/v1/send-fcm";
            var payload = new
            {
                user_id = userId.ToString(),
                title = "새 메시지",
                body = messagePreview,
                type = "chat_message",
                data = new Dictionary<string, string>
                {
                    { "sender_id", senderName }
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_supabaseKey}");

            var response = await _httpClient.PostAsync(url, content);
            Console.WriteLine($"[FCM] Notify user {userId}: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FCM] Error: {ex.Message}");
        }
    }
}
