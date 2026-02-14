using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace MilOps.Server.Models;

[Table("messages")]
public class ServerMessage : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("sender_id")]
    public Guid SenderId { get; set; }

    [Column("receiver_id")]
    public Guid ReceiverId { get; set; }

    [Column("content")]
    public string Content { get; set; } = "";

    [Column("is_read")]
    public bool IsRead { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }
}
