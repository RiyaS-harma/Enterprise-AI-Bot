namespace EnterpriseAIBot.Models;

public sealed class DocumentChunk
{
    public string Id { get; init; } = string.Empty;
    public string DocumentId { get; init; } = string.Empty;
    public string DocumentName { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string ChunkId { get; init; } = string.Empty;
    public int? PageNumber { get; init; }
    public string BlobPath { get; init; } = string.Empty;
    public string Department { get; init; } = string.Empty;
    public string DocumentType { get; init; } = string.Empty;
    public DateTimeOffset UploadDate { get; init; }
    public float[]? Embedding { get; set; }
    public double? RelevanceScore { get; set; }
}

