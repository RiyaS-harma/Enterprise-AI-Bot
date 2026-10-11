namespace EnterpriseAIBot.Data.Entities;

public sealed class ChatMessage
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public string Role { get; set; } = string.Empty;     // "user" | "assistant"
    public string Content { get; set; } = string.Empty;
    public string? SourcesJson { get; set; }             // JSON array of SourceDocument
    public DateTime CreatedAt { get; set; }

    public Conversation Conversation { get; set; } = null!;
}

