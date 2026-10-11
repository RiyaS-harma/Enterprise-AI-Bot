using EnterpriseAIBot.Models;

namespace EnterpriseAIBot.Services.Interfaces;

public interface IHistoryService
{
    Task<int> GetOrCreateConversationAsync(string sessionId, string? userId = null, CancellationToken ct = default);
    Task<int> CreateConversationAsync(string sessionId, string? userId = null, CancellationToken ct = default);
    Task SaveTurnAsync(int conversationId, string question, ChatResponse response, CancellationToken ct = default);
    Task<List<ConversationMessage>> GetHistoryAsync(string sessionId, string? userId = null, int maxTurns = 10, CancellationToken ct = default);
    Task<List<string>> GetRecentQuestionsAsync(string sessionId, string? userId = null, int maxQuestions = 8, CancellationToken ct = default);
    Task<List<SidebarChatHistoryItem>> GetRecentQuestionItemsAsync(string sessionId, string? userId = null, int maxQuestions = 8, CancellationToken ct = default);
    Task<List<SidebarChatHistoryItem>> GetRecentConversationItemsAsync(string sessionId, string? userId = null, int maxConversations = 8, CancellationToken ct = default);
    Task<List<ConversationMessage>> GetConversationMessagesAsync(int conversationId, string sessionId, string? userId = null, int maxTurns = 30, CancellationToken ct = default);
    Task<bool> CanAccessConversationAsync(int conversationId, string sessionId, string? userId = null, CancellationToken ct = default);
}

