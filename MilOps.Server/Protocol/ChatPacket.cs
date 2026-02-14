using System;
using Newtonsoft.Json;

namespace MilOps.Server.Protocol;

public class ChatPacket
{
    [JsonProperty("type")]
    public PacketType Type { get; set; }

    [JsonProperty("messageId")]
    public Guid? MessageId { get; set; }

    [JsonProperty("senderId")]
    public Guid? SenderId { get; set; }

    [JsonProperty("receiverId")]
    public Guid? ReceiverId { get; set; }

    [JsonProperty("content")]
    public string? Content { get; set; }

    [JsonProperty("timestamp")]
    public DateTimeOffset? Timestamp { get; set; }

    [JsonProperty("success")]
    public bool? Success { get; set; }

    [JsonProperty("error")]
    public string? Error { get; set; }
}
