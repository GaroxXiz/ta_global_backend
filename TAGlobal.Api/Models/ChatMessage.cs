namespace TAGlobal.Api.Models;

public sealed class ChatMessage
{
    public int Id { get; set; }
    public int ThreadId { get; set; }
    public ChatThread? Thread { get; set; }
    public string FromUserId { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
}
