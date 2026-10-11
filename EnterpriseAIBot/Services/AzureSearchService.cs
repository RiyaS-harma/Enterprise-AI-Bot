using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using EnterpriseAIBot.Configuration;
using EnterpriseAIBot.Models;
using EnterpriseAIBot.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnterpriseAIBot.Services;

public sealed class AzureSearchService : IAzureSearchService
{
    private readonly SearchClient _searchClient;
    private readonly SearchIndexClient _indexClient;
    private readonly AzureSearchOptions _options;
    private readonly ILogger<AzureSearchService> _logger;

    private const int VectorDimensions = 3072; // text-embedding-3-large
    private const string VectorFieldName = "embedding";

    public AzureSearchService(IOptions<AzureSearchOptions> options, ILogger<AzureSearchService> logger)
    {
        _options = options.Value;
        _logger = logger;
        var credential = new AzureKeyCredential(_options.ApiKey);
        var endpoint = new Uri(_options.Endpoint);
        _searchClient = new SearchClient(endpoint, _options.IndexName, credential);
        _indexClient = new SearchIndexClient(endpoint, credential);
    }

    public async Task EnsureIndexExistsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var indexResponse = await _indexClient.GetIndexAsync(_options.IndexName, cancellationToken);
            var index = indexResponse.Value;

            var requiredFields = new[]
            {
                "id", "documentId", "documentName", "content", "chunkId", "embedding"
            };

            var missingFields = requiredFields
                .Where(required => !index.Fields.Any(f => string.Equals(f.Name, required, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (missingFields.Count > 0)
            {
                _logger.LogWarning(
                    "Azure AI Search index '{IndexName}' is missing required fields: {MissingFields}. Recreating index to match current schema.",
                    _options.IndexName,
                    string.Join(", ", missingFields));

                await _indexClient.DeleteIndexAsync(_options.IndexName, cancellationToken);
                await _indexClient.CreateIndexAsync(BuildIndexDefinition(_options.IndexName), cancellationToken);
                _logger.LogInformation("Azure AI Search index '{IndexName}' recreated with expected schema.", _options.IndexName);
                return;
            }

            _logger.LogInformation("Azure AI Search index '{IndexName}' already exists.", _options.IndexName);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogInformation("Creating Azure AI Search index '{IndexName}'.", _options.IndexName);
            await _indexClient.CreateIndexAsync(BuildIndexDefinition(_options.IndexName), cancellationToken);
        }
    }

    public async Task<bool> DocumentExistsAsync(string documentId, CancellationToken cancellationToken = default)
    {
        var options = new SearchOptions { Filter = $"documentId eq '{documentId}'", Size = 1 };
        try
        {
            var results = await _searchClient.SearchAsync<SearchDocument>("*", options, cancellationToken);
            await foreach (var _ in results.Value.GetResultsAsync())
                return true;
            return false;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogWarning(ex, "Document existence check failed for documentId '{DocumentId}'. Assuming document is not indexed.", documentId);
            return false;
        }
    }

    public async Task IndexChunksAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        var documents = chunks.Select(MapToSearchDocument).ToList();
        var batch = IndexDocumentsBatch.Upload(documents);
        var result = await _searchClient.IndexDocumentsAsync(batch, cancellationToken: cancellationToken);
        _logger.LogInformation("Indexed {Count} chunks.", result.Value.Results.Count);
    }

    public async Task<List<DocumentChunk>> HybridSearchAsync(string query, float[] embedding, CancellationToken cancellationToken = default)
    {
        var vectorQuery = new VectorizedQuery(embedding)
        {
            KNearestNeighborsCount = _options.TopK,
            Fields = { VectorFieldName }
        };

        var searchOptions = new SearchOptions
        {
            VectorSearch = new() { Queries = { vectorQuery } },
            Size = _options.TopK,
            Select = { "id", "documentId", "documentName", "content", "chunkId", "pageNumber", "blobPath", "department", "documentType", "uploadDate" },
            QueryType = SearchQueryType.Semantic,
            SemanticSearch = new SemanticSearchOptions { SemanticConfigurationName = "default" }
        };

        var response = await _searchClient.SearchAsync<SearchDocument>(query, searchOptions, cancellationToken);
        var chunks = new List<DocumentChunk>();

        await foreach (var result in response.Value.GetResultsAsync())
        {
            chunks.Add(MapToDocumentChunk(result.Document, result.Score ?? 0));
        }

        _logger.LogInformation("Hybrid search returned {Count} results.", chunks.Count);
        return chunks;
    }

    private static SearchDocument MapToSearchDocument(DocumentChunk chunk) => new()
    {
        ["id"] = chunk.Id,
        ["documentId"] = chunk.DocumentId,
        ["documentName"] = chunk.DocumentName,
        ["content"] = chunk.Content,
        ["chunkId"] = chunk.ChunkId,
        ["pageNumber"] = chunk.PageNumber,
        ["blobPath"] = chunk.BlobPath,
        ["department"] = chunk.Department,
        ["documentType"] = chunk.DocumentType,
        ["uploadDate"] = chunk.UploadDate,
        [VectorFieldName] = chunk.Embedding
    };

    private static DocumentChunk MapToDocumentChunk(SearchDocument doc, double score) => new()
    {
        Id = doc["id"]?.ToString() ?? string.Empty,
        DocumentId = doc["documentId"]?.ToString() ?? string.Empty,
        DocumentName = doc["documentName"]?.ToString() ?? string.Empty,
        Content = doc["content"]?.ToString() ?? string.Empty,
        ChunkId = doc["chunkId"]?.ToString() ?? string.Empty,
        PageNumber = doc.TryGetValue("pageNumber", out var page) && page is int p ? p : null,
        BlobPath = doc["blobPath"]?.ToString() ?? string.Empty,
        Department = doc["department"]?.ToString() ?? string.Empty,
        DocumentType = doc["documentType"]?.ToString() ?? string.Empty,
        UploadDate = doc.TryGetValue("uploadDate", out var date) && date is DateTimeOffset d ? d : DateTimeOffset.UtcNow,
        RelevanceScore = score
    };

    private static SearchIndex BuildIndexDefinition(string indexName)
    {
        var vectorConfig = new VectorSearch();
        vectorConfig.Algorithms.Add(new HnswAlgorithmConfiguration("hnsw-config"));
        vectorConfig.Profiles.Add(new VectorSearchProfile("vector-profile", "hnsw-config"));

        var semanticConfig = new SemanticConfiguration("default", new SemanticPrioritizedFields
        {
            ContentFields = { new SemanticField("content") },
            KeywordsFields = { new SemanticField("documentName") }
        });

        var index = new SearchIndex(indexName)
        {
            VectorSearch = vectorConfig,
            SemanticSearch = new SemanticSearch { Configurations = { semanticConfig } }
        };

        SearchField[] fields =
        [
            new SimpleField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
            new SimpleField("documentId", SearchFieldDataType.String) { IsFilterable = true },
            new SearchableField("documentName") { IsFilterable = true, IsSortable = true },
            new SearchableField("content"),
            new SimpleField("chunkId", SearchFieldDataType.String) { IsFilterable = true },
            new SimpleField("pageNumber", SearchFieldDataType.Int32) { IsFilterable = true },
            new SimpleField("blobPath", SearchFieldDataType.String),
            new SimpleField("department", SearchFieldDataType.String) { IsFilterable = true, IsFacetable = true },
            new SimpleField("documentType", SearchFieldDataType.String) { IsFilterable = true, IsFacetable = true },
            new SimpleField("uploadDate", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true },
            new VectorSearchField(VectorFieldName, VectorDimensions, "vector-profile")
        ];
        foreach (var field in fields) index.Fields.Add(field);

        return index;
    }
}

