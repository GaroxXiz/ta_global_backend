namespace TAGlobal.Api.Models;

public sealed class ChatThread
{
    public int Id { get; set; }
    public required string ThreadKey { get; set; }
    public required string With { get; set; }
    public string Role { get; set; } = "seller";
    public string Subject { get; set; } = "";
    public string Party { get; set; } = "";
    public int Unread { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
