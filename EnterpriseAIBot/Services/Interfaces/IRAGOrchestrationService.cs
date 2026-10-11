using EnterpriseAIBot.Models;

namespace EnterpriseAIBot.Services.Interfaces;

public interface IRAGOrchestrationService
{
    Task<ChatResponse> AskAsync(ChatRequest request, CancellationToken cancellationToken = default);
}

