using System;

namespace MilOps.Models;

/// <summary>
/// SQLite 로컬 캐시용 메시지 (채팅 기록 저장)
/// </summary>
[SQLite.Table("cached_messages")]
public class LocalChatMessage
{
    [SQLite.PrimaryKey]
    public string Id { get; set; } = "";

    public string SenderId { get; set; } = "";

    public string ReceiverId { get; set; } = "";

    public string Content { get; set; } = "";

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 로컬 큐 — 서버 다운 시 미전송 메시지 임시 보관
/// 서버 복구 시 QueueOrder 순서대로 전송
/// </summary>
[SQLite.Table("pending_messages")]
public class PendingMessage
{
    [SQLite.PrimaryKey, SQLite.AutoIncrement]
    public int QueueOrder { get; set; }

    public string ReceiverId { get; set; } = "";

    public string Content { get; set; } = "";

    public DateTime QueuedAt { get; set; }
}
