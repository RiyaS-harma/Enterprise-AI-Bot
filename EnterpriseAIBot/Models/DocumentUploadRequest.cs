using System.ComponentModel.DataAnnotations;

namespace EnterpriseAIBot.Models;

public sealed class DocumentUploadRequest
{
    [Required]
    public IFormFile File { get; init; } = null!;

    [StringLength(100)]
    public string Department { get; init; } = string.Empty;

    [StringLength(100)]
    public string DocumentType { get; init; } = string.Empty;
}

public sealed class DocumentUploadResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? DocumentId { get; init; }
    public int ChunksIndexed { get; init; }

    public static DocumentUploadResult Ok(string documentId, int chunks) =>
        new() { Success = true, Message = "Document uploaded and indexed successfully.", DocumentId = documentId, ChunksIndexed = chunks };

    public static DocumentUploadResult Fail(string message) =>
        new() { Success = false, Message = message };
}

