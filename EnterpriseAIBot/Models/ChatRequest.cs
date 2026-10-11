using System.ComponentModel.DataAnnotations;

namespace EnterpriseAIBot.Models;

public sealed record ChatRequest
{
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public string Question { get; init; } = string.Empty;

    public List<ConversationMessage> History { get; init; } = [];
}

public sealed class ConversationMessage
{
    public string Role { get; init; } = string.Empty; // "user" or "assistant"
    public string Content { get; init; } = string.Empty;
}

