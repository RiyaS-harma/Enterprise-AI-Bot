using System.Text;
using System.Text.RegularExpressions;
using EnterpriseAIBot.AI.SemanticKernel;
using EnterpriseAIBot.Configuration;
using EnterpriseAIBot.Models;
using EnterpriseAIBot.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnterpriseAIBot.Services;

public sealed partial class RAGOrchestrationService : IRAGOrchestrationService
{
    private readonly IAzureOpenAIService _openAIService;
    private readonly IAzureSearchService _searchService;
    private readonly SemanticKernelService _semanticKernelService;
    private readonly double _relevanceThreshold;
    private readonly ILogger<RAGOrchestrationService> _logger;

    public RAGOrchestrationService(
        IAzureOpenAIService openAIService,
        IAzureSearchService searchService,
        SemanticKernelService semanticKernelService,
        IOptions<AzureSearchOptions> searchOptions,
        ILogger<RAGOrchestrationService> logger)
    {
        _openAIService = openAIService;
        _searchService = searchService;
        _semanticKernelService = semanticKernelService;
        _relevanceThreshold = searchOptions.Value.RelevanceThreshold;
        _logger = logger;
    }

    public async Task<ChatResponse> AskAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("RAG pipeline started for query: {Query}", request.Question[..Math.Min(50, request.Question.Length)]);

        try
        {
            var sanitizedQuery = SanitizeQuery(request.Question);
            if (string.IsNullOrWhiteSpace(sanitizedQuery))
            {
                _logger.LogWarning("Query was empty after sanitization.");
                return ChatResponse.Error("Please enter a valid question.");
            }

            var embedding = await _openAIService.GenerateEmbeddingAsync(sanitizedQuery, cancellationToken);
            _logger.LogInformation("Embedding generated. Dimensions: {Dims}", embedding.Length);

            var retrievedChunks = await _searchService.HybridSearchAsync(sanitizedQuery, embedding, cancellationToken);
            _logger.LogInformation("Retrieved {Count} chunks from Azure AI Search.", retrievedChunks.Count);

            var filteredChunks = retrievedChunks
                .Where(c => c.RelevanceScore >= _relevanceThreshold)
                .OrderByDescending(c => c.RelevanceScore)
                .ToList();

            _logger.LogInformation(
                "Relevance filter: {Passed}/{Total} chunks passed threshold {Threshold}.",
                filteredChunks.Count,
                retrievedChunks.Count,
                _relevanceThreshold);

            if (filteredChunks.Count == 0 && retrievedChunks.Count > 0)
            {
                // Fallback to best available chunks to avoid over-filtering when scores are compressed.
                filteredChunks = retrievedChunks
                    .OrderByDescending(c => c.RelevanceScore ?? 0)
                    .Take(3)
                    .ToList();

                _logger.LogInformation(
                    "No chunks passed threshold {Threshold}; using top {Count} fallback chunks.",
                    _relevanceThreshold,
                    filteredChunks.Count);
            }

            if (filteredChunks.Count == 0)
                return ChatResponse.NotFound();

            var answer = await _semanticKernelService.GetChatCompletionAsync(
                BuildSystemPrompt(),
                BuildUserPrompt(sanitizedQuery, filteredChunks),
                request.History,
                cancellationToken);

            var citations = filteredChunks
                .GroupBy(c => new { c.DocumentName, c.PageNumber })
                .Select(g => new SourceDocument
                {
                    DocumentName = g.Key.DocumentName,
                    PageNumber = g.Key.PageNumber,
                    Department = g.First().Department,
                    DocumentType = g.First().DocumentType,
                    RelevanceScore = g.Max(c => c.RelevanceScore ?? 0)
                })
                .OrderByDescending(s => s.RelevanceScore)
                .ToList();

            _logger.LogInformation("RAG pipeline completed successfully. Citations: {Count}", citations.Count);
            return ChatResponse.Success(answer, citations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RAG pipeline failed.");
            return ChatResponse.Error("An error occurred while processing your request. Please try again.");
        }
    }

    private static string BuildSystemPrompt() =>
        """
        You are a helpful internal assistant for Enterprise AI Bot users.
        Answer questions ONLY using the document context provided below.
        If the exact answer (for example, a number/count) is not present but related policy context exists,
        explain what is available from the context and clearly state that the exact value is not specified.
        If the context is not relevant to the question at all, respond with exactly:
        "I could not find sufficient information in the available documents to answer your question."
        Do not make up information. Do not use external knowledge. Be concise and professional.
        """;

    private static string BuildUserPrompt(string sanitizedQuery, IEnumerable<DocumentChunk> filteredChunks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("DOCUMENT CONTEXT:");
        sb.AppendLine("---");

        foreach (var chunk in filteredChunks)
        {
            sb.AppendLine($"[Source: {chunk.DocumentName}, Page: {chunk.PageNumber?.ToString() ?? "N/A"}]");
            sb.AppendLine(chunk.Content);
            sb.AppendLine("---");
        }

        sb.AppendLine($"QUESTION: {sanitizedQuery}");
        return sb.ToString();
    }

    private static string SanitizeQuery(string query)
    {
        var cleaned = ControlCharRegex().Replace(query, " ");
        cleaned = WhitespaceRegex().Replace(cleaned.Trim(), " ");
        return cleaned.Length > 1000 ? cleaned[..1000] : cleaned;
    }

    [GeneratedRegex(@"[\x00-\x1F\x7F]")]
    private static partial Regex ControlCharRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}

