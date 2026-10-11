using System.Text.Json;
using EnterpriseAIBot.Data;
using EnterpriseAIBot.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnterpriseAIBot.AI.Tools;

/// <summary>
/// MCP Tool: GetChatHistory
/// Returns recent conversation turns for a session.
/// </summary>
public sealed class GetChatHistoryTool : IMcpTool
{
    private readonly IHistoryService _historyService;
    private readonly ILogger<GetChatHistoryTool> _logger;

    public string ToolName => "GetChatHistory";

    public GetChatHistoryTool(IHistoryService historyService, ILogger<GetChatHistoryTool> logger)
    {
        _historyService = historyService;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(Dictionary<string, string> parameters, CancellationToken ct = default)
    {
        if (!parameters.TryGetValue("sessionId", out var sessionId) || string.IsNullOrWhiteSpace(sessionId))
            return JsonSerializer.Serialize(new { error = "Parameter 'sessionId' is required." });

        var maxTurns = parameters.TryGetValue("maxTurns", out var mt) && int.TryParse(mt, out var n) ? n : 10;

        _logger.LogInformation("MCP GetChatHistory called for session {SessionId}.", sessionId);

        var history = await _historyService.GetHistoryAsync(sessionId, null, maxTurns, ct);
        return JsonSerializer.Serialize(history);
    }
}

/// <summary>
/// MCP Tool: GetDocumentSources
/// Returns the source citations stored with a specific assistant message.
/// </summary>
public sealed class GetDocumentSourcesTool : IMcpTool
{
    private readonly EnterpriseAIBotDbContext _db;
    private readonly ILogger<GetDocumentSourcesTool> _logger;

    public string ToolName => "GetDocumentSources";

    public GetDocumentSourcesTool(EnterpriseAIBotDbContext db, ILogger<GetDocumentSourcesTool> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(Dictionary<string, string> parameters, CancellationToken ct = default)
    {
        if (!parameters.TryGetValue("sessionId", out var sessionId))
            return JsonSerializer.Serialize(new { error = "Parameter 'sessionId' is required." });

        _logger.LogInformation("MCP GetDocumentSources called for session {SessionId}.", sessionId);

        var lastSources = _db.ChatMessages
            .Where(m => m.Conversation.SessionId == sessionId && m.Role == "assistant" && m.SourcesJson != null)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => m.SourcesJson)
            .AsEnumerable()
            .FirstOrDefault();

        return lastSources ?? JsonSerializer.Serialize(Array.Empty<object>());
    }
}

