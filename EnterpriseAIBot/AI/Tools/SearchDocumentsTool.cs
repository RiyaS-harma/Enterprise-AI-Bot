using System.Text.Json;
using EnterpriseAIBot.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace EnterpriseAIBot.AI.Tools;

/// <summary>
/// MCP Tool: SearchDocuments
/// Performs a keyword search against Azure AI Search and returns chunk summaries.
/// </summary>
public sealed class SearchDocumentsTool : IMcpTool
{
    private readonly IAzureSearchService _searchService;
    private readonly IAzureOpenAIService _openAIService;
    private readonly ILogger<SearchDocumentsTool> _logger;

    public string ToolName => "SearchDocuments";

    public SearchDocumentsTool(
        IAzureSearchService searchService,
        IAzureOpenAIService openAIService,
        ILogger<SearchDocumentsTool> logger)
    {
        _searchService = searchService;
        _openAIService = openAIService;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(Dictionary<string, string> parameters, CancellationToken ct = default)
    {
        if (!parameters.TryGetValue("query", out var query) || string.IsNullOrWhiteSpace(query))
            return JsonSerializer.Serialize(new { error = "Parameter 'query' is required." });

        _logger.LogInformation("MCP SearchDocuments called with query: {Query}", query);

        var embedding = await _openAIService.GenerateEmbeddingAsync(query, ct);
        var chunks = await _searchService.HybridSearchAsync(query, embedding, ct);

        var results = chunks.Select(c => new
        {
            c.DocumentName,
            c.PageNumber,
            c.Department,
            Excerpt = c.Content.Length > 200 ? c.Content[..200] + "..." : c.Content,
            c.RelevanceScore
        });

        return JsonSerializer.Serialize(results);
    }
}

