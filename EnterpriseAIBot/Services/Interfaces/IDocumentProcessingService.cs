using EnterpriseAIBot.Models;

namespace EnterpriseAIBot.Services.Interfaces;

public interface IDocumentProcessingService
{
    Task<List<DocumentChunk>> ProcessDocumentAsync(
        Stream fileStream,
        string fileName,
        string blobPath,
        string department,
        string documentType,
        CancellationToken cancellationToken = default);
}

