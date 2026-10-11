using System.Text.Json;
using EnterpriseAIBot.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnterpriseAIBot.AI.Tools;

/// <summary>
/// MCP Tool: GetDocumentMetadata
/// Returns stored metadata for a document by name or documentId.
/// </summary>
public sealed class GetDocumentMetadataTool : IMcpTool
{
    private readonly EnterpriseAIBotDbContext _db;
    private readonly ILogger<GetDocumentMetadataTool> _logger;

    public string ToolName => "GetDocumentMetadata";

    public GetDocumentMetadataTool(EnterpriseAIBotDbContext db, ILogger<GetDocumentMetadataTool> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(Dictionary<string, string> parameters, CancellationToken ct = default)
    {
        parameters.TryGetValue("documentId", out var documentId);
        parameters.TryGetValue("documentName", out var documentName);

        if (string.IsNullOrWhiteSpace(documentId) && string.IsNullOrWhiteSpace(documentName))
            return JsonSerializer.Serialize(new { error = "Provide 'documentId' or 'documentName'." });

        _logger.LogInformation("MCP GetDocumentMetadata called.");

        var query = _db.DocumentMetadata.AsQueryable();

        if (!string.IsNullOrWhiteSpace(documentId))
            query = query.Where(d => d.DocumentId == documentId);
        else
            query = query.Where(d => d.DocumentName.Contains(documentName!));

        var doc = await query.FirstOrDefaultAsync(ct);

        if (doc is null)
            return JsonSerializer.Serialize(new { error = "Document not found." });

        return JsonSerializer.Serialize(new
        {
            doc.DocumentId,
            doc.DocumentName,
            doc.Department,
            doc.DocumentType,
            doc.ChunksIndexed,
            doc.UploadedAt
        });
    }
}

