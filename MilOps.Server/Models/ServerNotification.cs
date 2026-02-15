using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace MilOps.Server.Models;

[Table("notifications")]
public class ServerNotification : BaseModel
{
    [PrimaryKey("id", false)]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("type")]
    public string Type { get; set; } = "";

    [Column("title")]
    public string Title { get; set; } = "";

    [Column("message")]
    public string Message { get; set; } = "";

    [Column("is_read")]
    public bool IsRead { get; set; } = false;

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }
}
