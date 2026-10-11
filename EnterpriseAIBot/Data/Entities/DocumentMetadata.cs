namespace EnterpriseAIBot.Data.Entities;

public sealed class DocumentMetadata
{
    public int Id { get; set; }
    public string DocumentId { get; set; } = string.Empty;   // matches Azure Search documentId
    public string DocumentName { get; set; } = string.Empty;
    public string BlobPath { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public int ChunksIndexed { get; set; }
    public DateTime UploadedAt { get; set; }
}

