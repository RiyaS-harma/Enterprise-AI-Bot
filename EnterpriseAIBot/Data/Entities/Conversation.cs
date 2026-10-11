namespace EnterpriseAIBot.Data.Entities;

public sealed class Conversation
{
    public int Id { get; set; }
    public string SessionId { get; set; } = string.Empty;   // browser session / user identifier
    public string UserId { get; set; } = string.Empty;      // Entra ID object ID when auth is added
    public DateTime StartedAt { get; set; }
    public DateTime LastActivityAt { get; set; }

    public ICollection<ChatMessage> Messages { get; set; } = [];
}

