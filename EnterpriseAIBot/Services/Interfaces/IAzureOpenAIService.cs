using EnterpriseAIBot.Models;

namespace EnterpriseAIBot.Services.Interfaces;

public interface IAzureOpenAIService
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    Task<string> GetChatCompletionAsync(
        string systemPrompt,
        string userPrompt,
        IEnumerable<ConversationMessage> history,
        CancellationToken cancellationToken = default);
}

