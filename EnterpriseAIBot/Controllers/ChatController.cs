using EnterpriseAIBot.Data.Entities;
using EnterpriseAIBot.Models;
using EnterpriseAIBot.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseAIBot.Controllers;

[Authorize(Policy = "EmployeeOrAdmin")]
public sealed class ChatController : Controller
{
    private readonly IRAGOrchestrationService _ragService;
    private readonly IHistoryService _historyService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IRAGOrchestrationService ragService,
        IHistoryService historyService,
        ILogger<ChatController> logger)
    {
        _ragService = ragService;
        _historyService = historyService;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (User?.Identity?.IsAuthenticated ?? false)
        {
            var sessionId = GetOrCreateSessionId();
            var userId = GetCurrentUserId();

            if (User.IsInRole("Employee"))
            {
                ViewData["SidebarChatHistory"] = await _historyService.GetRecentConversationItemsAsync(
                    sessionId,
                    userId,
                    ct: cancellationToken);
            }

            await EnsureActiveConversationAsync(sessionId, userId, cancellationToken);
        }

        return View();
    }

    [HttpPost("api/chat")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ask([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { error = "Invalid request." });

        try
        {
            var sessionId = GetOrCreateSessionId();
            var userId = GetCurrentUserId();
            var conversationId = await EnsureActiveConversationAsync(sessionId, userId, cancellationToken);

            // Load history from SQL if client didn't send it
            if (request.History.Count == 0)
                request = request with
                {
                    History = await _historyService.GetConversationMessagesAsync(
                        conversationId,
                        sessionId,
                        userId,
                        ct: cancellationToken)
                };

            var response = await _ragService.AskAsync(request, cancellationToken);

            // Persist turn to SQL Server
            await _historyService.SaveTurnAsync(conversationId, request.Question, response, cancellationToken);

            return Ok(new
            {
                answer = response.Answer,
                sources = response.Sources,
                isError = response.IsError,
                errorMessage = response.ErrorMessage,
                conversationId
            });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in chat endpoint.");
            return StatusCode(500, ChatResponse.Error("An unexpected error occurred. Please try again."));
        }
    }

    [HttpPost("api/chat/new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartNewChat(CancellationToken cancellationToken)
    {
        try
        {
            var sessionId = GetOrCreateSessionId();
            var userId = GetCurrentUserId();
            var conversationId = await _historyService.CreateConversationAsync(sessionId, userId, cancellationToken);

            SetActiveConversationId(conversationId);
            return Ok(new { success = true, conversationId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start a new chat conversation.");
            return StatusCode(500, new { success = false, message = "Failed to start a new chat." });
        }
    }

    [HttpGet("api/chat/history/{conversationId:int}")]
    public async Task<IActionResult> GetConversationHistory(int conversationId, CancellationToken cancellationToken)
    {
        try
        {
            var sessionId = GetOrCreateSessionId();
            var userId = GetCurrentUserId();

            var messages = await _historyService.GetConversationMessagesAsync(
                conversationId,
                sessionId,
                userId,
                ct: cancellationToken);

            return Ok(new { success = true, messages });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error while loading conversation history for {ConversationId}.", conversationId);
            return StatusCode(500, new { success = false, message = "Failed to load conversation history." });
        }
    }

    private string GetOrCreateSessionId()
    {
        const string key = "ChatSessionId";
        if (HttpContext.Session.GetString(key) is { } existing)
            return existing;

        var newId = Guid.NewGuid().ToString("N");
        HttpContext.Session.SetString(key, newId);
        return newId;
    }

    private string? GetCurrentUserId() => User?.Identity?.Name;

    private async Task<int> EnsureActiveConversationAsync(string sessionId, string? userId, CancellationToken cancellationToken)
    {
        const string key = "ActiveConversationId";

        if (int.TryParse(HttpContext.Session.GetString(key), out var existingConversationId)
            && existingConversationId > 0
            && await _historyService.CanAccessConversationAsync(existingConversationId, sessionId, userId, cancellationToken))
        {
            return existingConversationId;
        }

        var conversationId = await _historyService.CreateConversationAsync(sessionId, userId, cancellationToken);
        SetActiveConversationId(conversationId);
        return conversationId;
    }

    private void SetActiveConversationId(int conversationId)
        => HttpContext.Session.SetString("ActiveConversationId", conversationId.ToString());
}

