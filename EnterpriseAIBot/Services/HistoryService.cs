using System.Text.Json;
using EnterpriseAIBot.Data;
using EnterpriseAIBot.Data.Entities;
using EnterpriseAIBot.Models;
using EnterpriseAIBot.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnterpriseAIBot.Services;

public sealed class HistoryService : IHistoryService
{
    private readonly EnterpriseAIBotDbContext _db;
    private readonly ILogger<HistoryService> _logger;

    public HistoryService(EnterpriseAIBotDbContext db, ILogger<HistoryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<int> GetOrCreateConversationAsync(string sessionId, string? userId = null, CancellationToken ct = default)
    {
        var normalizedUserId = NormalizeUserId(userId);

        var conversation = await FindConversationQuery(sessionId, normalizedUserId)
            .FirstOrDefaultAsync(ct);

        if (conversation is null)
        {
            conversation = new Conversation
            {
                SessionId = sessionId,
                UserId = normalizedUserId,
                StartedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow
            };
            _db.Conversations.Add(conversation);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Created new conversation for session {SessionId}.", sessionId);
        }
        else if (!string.IsNullOrWhiteSpace(normalizedUserId) && !string.Equals(conversation.UserId, normalizedUserId, StringComparison.Ordinal))
        {
            conversation.UserId = normalizedUserId;
            await _db.SaveChangesAsync(ct);
        }

        return conversation.Id;
    }

    public async Task<int> CreateConversationAsync(string sessionId, string? userId = null, CancellationToken ct = default)
    {
        var conversation = new Conversation
        {
            SessionId = sessionId,
            UserId = NormalizeUserId(userId),
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        };

        _db.Conversations.Add(conversation);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Created conversation {ConversationId}.", conversation.Id);
        return conversation.Id;
    }

    public async Task SaveTurnAsync(int conversationId, string question, ChatResponse response, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        _db.ChatMessages.Add(new ChatMessage
        {
            ConversationId = conversationId,
            Role = "user",
            Content = question,
            CreatedAt = now
        });

        _db.ChatMessages.Add(new ChatMessage
        {
            ConversationId = conversationId,
            Role = "assistant",
            Content = response.Answer,
            SourcesJson = response.Sources.Count > 0
                ? JsonSerializer.Serialize(response.Sources)
                : null,
            CreatedAt = now
        });

        await _db.Conversations
            .Where(c => c.Id == conversationId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.LastActivityAt, now), ct);

        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<ConversationMessage>> GetHistoryAsync(string sessionId, string? userId = null, int maxTurns = 10, CancellationToken ct = default)
    {
        var normalizedUserId = NormalizeUserId(userId);

        var messages = await _db.ChatMessages
            .Where(m => FindConversationQuery(sessionId, normalizedUserId).Select(c => c.Id).Contains(m.ConversationId))
            .OrderByDescending(m => m.CreatedAt)
            .Take(maxTurns * 2)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ConversationMessage { Role = m.Role, Content = m.Content })
            .ToListAsync(ct);

        return messages;
    }

    public async Task<List<string>> GetRecentQuestionsAsync(string sessionId, string? userId = null, int maxQuestions = 8, CancellationToken ct = default)
    {
        var normalizedUserId = NormalizeUserId(userId);

        return await _db.ChatMessages
            .Where(m => m.Role == "user")
            .Where(m => FindConversationQuery(sessionId, normalizedUserId).Select(c => c.Id).Contains(m.ConversationId))
            .OrderByDescending(m => m.CreatedAt)
            .Take(maxQuestions)
            .Select(m => m.Content)
            .ToListAsync(ct);
    }

    public async Task<List<SidebarChatHistoryItem>> GetRecentQuestionItemsAsync(string sessionId, string? userId = null, int maxQuestions = 8, CancellationToken ct = default)
    {
        var normalizedUserId = NormalizeUserId(userId);

        return await _db.ChatMessages
            .Where(m => m.Role == "user")
            .Where(m => FindConversationQuery(sessionId, normalizedUserId).Select(c => c.Id).Contains(m.ConversationId))
            .OrderByDescending(m => m.CreatedAt)
            .Take(maxQuestions)
            .Select(m => new SidebarChatHistoryItem
            {
                ConversationId = m.ConversationId,
                Preview = m.Content
            })
            .ToListAsync(ct);
    }

    public async Task<List<SidebarChatHistoryItem>> GetRecentConversationItemsAsync(string sessionId, string? userId = null, int maxConversations = 8, CancellationToken ct = default)
    {
        var normalizedUserId = NormalizeUserId(userId);

        return await FindConversationQuery(sessionId, normalizedUserId)
            .OrderByDescending(c => c.LastActivityAt)
            .Take(maxConversations)
            .Select(c => new SidebarChatHistoryItem
            {
                ConversationId = c.Id,
                Preview = c.Messages
                    .Where(m => m.Role == "user")
                    .OrderBy(m => m.CreatedAt)
                    .Select(m => m.Content)
                    .FirstOrDefault() ?? "(No messages)"
            })
            .ToListAsync(ct);
    }

    public async Task<bool> CanAccessConversationAsync(int conversationId, string sessionId, string? userId = null, CancellationToken ct = default)
    {
        var normalizedUserId = NormalizeUserId(userId);
        return await FindConversationQuery(sessionId, normalizedUserId)
            .AnyAsync(c => c.Id == conversationId, ct);
    }

    public async Task<List<ConversationMessage>> GetConversationMessagesAsync(int conversationId, string sessionId, string? userId = null, int maxTurns = 30, CancellationToken ct = default)
    {
        var normalizedUserId = NormalizeUserId(userId);

        var isAuthorized = await CanAccessConversationAsync(conversationId, sessionId, normalizedUserId, ct);

        if (!isAuthorized)
        {
            _logger.LogWarning("Rejected history access for conversation {ConversationId}.", conversationId);
            return [];
        }

        return await _db.ChatMessages
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(maxTurns * 2)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ConversationMessage
            {
                Role = m.Role,
                Content = m.Content
            })
            .ToListAsync(ct);
    }

    private IQueryable<Conversation> FindConversationQuery(string sessionId, string? userId)
    {
        if (!string.IsNullOrWhiteSpace(userId))
            return _db.Conversations.Where(c => c.UserId == userId);

        return _db.Conversations.Where(c => c.SessionId == sessionId);
    }

    private static string NormalizeUserId(string? userId) => userId?.Trim() ?? string.Empty;
}

