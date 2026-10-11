using EnterpriseAIBot.Models;

namespace EnterpriseAIBot.Services.Interfaces;

public interface IAzureSearchService
{
    Task<List<DocumentChunk>> HybridSearchAsync(string query, float[] embedding, CancellationToken cancellationToken = default);
    Task IndexChunksAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default);
    Task<bool> DocumentExistsAsync(string documentId, CancellationToken cancellationToken = default);
    Task EnsureIndexExistsAsync(CancellationToken cancellationToken = default);
}

