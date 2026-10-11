using EnterpriseAIBot.Configuration;
using EnterpriseAIBot.Data;
using EnterpriseAIBot.Data.Entities;
using EnterpriseAIBot.Models;
using EnterpriseAIBot.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EnterpriseAIBot.Controllers;

[Authorize(Policy = "AdminOnly")]
public sealed class DocumentController : Controller
{
    private readonly IBlobStorageService _blobService;
    private readonly IDocumentProcessingService _processingService;
    private readonly IAzureOpenAIService _openAIService;
    private readonly IAzureSearchService _searchService;
    private readonly EnterpriseAIBotDbContext _db;
    private readonly BlobStorageOptions _blobOptions;
    private readonly ILogger<DocumentController> _logger;

    public DocumentController(
        IBlobStorageService blobService,
        IDocumentProcessingService processingService,
        IAzureOpenAIService openAIService,
        IAzureSearchService searchService,
        EnterpriseAIBotDbContext db,
        IOptions<BlobStorageOptions> blobOptions,
        ILogger<DocumentController> logger)
    {
        _blobService = blobService;
        _processingService = processingService;
        _openAIService = openAIService;
        _searchService = searchService;
        _db = db;
        _blobOptions = blobOptions.Value;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Upload() => View();

    [HttpPost("api/documents/upload")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload([FromForm] DocumentUploadRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(DocumentUploadResult.Fail("Invalid request."));

        var validationError = ValidateFile(request.File);
        if (validationError is not null)
            return BadRequest(DocumentUploadResult.Fail(validationError));

        try
        {
            // Upload to Blob Storage
            await using var stream = request.File.OpenReadStream();
            var blobPath = await _blobService.UploadAsync(stream, request.File.FileName, request.File.ContentType, cancellationToken);

            // Extract text and split into chunks
            await using var processingStream = request.File.OpenReadStream();
            var chunks = await _processingService.ProcessDocumentAsync(
                processingStream, request.File.FileName, blobPath,
                request.Department, request.DocumentType, cancellationToken);

            if (chunks.Count == 0)
                return BadRequest(DocumentUploadResult.Fail("No text could be extracted from the document."));

            // Check incremental indexing - skip if already indexed
            var documentId = chunks[0].DocumentId;
            if (await _searchService.DocumentExistsAsync(documentId, cancellationToken))
            {
                _logger.LogInformation("Document {DocumentId} already indexed. Skipping.", documentId);
                return Ok(DocumentUploadResult.Ok(documentId, 0));
            }

            // Generate embeddings for each chunk
            foreach (var chunk in chunks)
            {
                chunk.Embedding = await _openAIService.GenerateEmbeddingAsync(chunk.Content, cancellationToken);
            }

            // Index chunks in Azure AI Search
            await _searchService.IndexChunksAsync(chunks, cancellationToken);

            // Persist metadata to SQL Server
            _db.DocumentMetadata.Add(new DocumentMetadata
            {
                DocumentId = documentId,
                DocumentName = request.File.FileName,
                BlobPath = blobPath,
                Department = request.Department,
                DocumentType = request.DocumentType,
                ChunksIndexed = chunks.Count,
                UploadedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Document '{FileName}' indexed with {Count} chunks.", request.File.FileName, chunks.Count);
            return Ok(DocumentUploadResult.Ok(documentId, chunks.Count));
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(DocumentUploadResult.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process document '{FileName}'.", request.File.FileName);
            return StatusCode(500, DocumentUploadResult.Fail("An error occurred while processing the document."));
        }
    }

    [HttpPost("api/documents/sync-existing")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SyncExisting(CancellationToken cancellationToken)
    {
        try
        {
            var existingBlobPaths = await _db.DocumentMetadata
                .Select(m => m.BlobPath)
                .ToHashSetAsync(cancellationToken);

            var allBlobPaths = await _blobService.ListBlobPathsAsync(cancellationToken);
            var candidates = allBlobPaths
                .Where(IsAllowedBlobPath)
                .ToList();

            var processed = 0;
            var skipped = 0;
            var failed = 0;

            foreach (var blobPath in candidates)
            {
                if (existingBlobPaths.Contains(blobPath))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    var fileName = Path.GetFileName(blobPath);
                    await using var stream = await _blobService.DownloadAsync(blobPath, cancellationToken);

                    var chunks = await _processingService.ProcessDocumentAsync(
                        stream,
                        fileName,
                        blobPath,
                        "Unknown",
                        "Backfill",
                        cancellationToken);

                    if (chunks.Count == 0)
                    {
                        skipped++;
                        _logger.LogWarning("Skipping blob '{BlobPath}' because no text could be extracted.", blobPath);
                        continue;
                    }

                    foreach (var chunk in chunks)
                    {
                        chunk.Embedding = await _openAIService.GenerateEmbeddingAsync(chunk.Content, cancellationToken);
                    }

                    await _searchService.IndexChunksAsync(chunks, cancellationToken);

                    var documentId = chunks[0].DocumentId;
                    _db.DocumentMetadata.Add(new DocumentMetadata
                    {
                        DocumentId = documentId,
                        DocumentName = fileName,
                        BlobPath = blobPath,
                        Department = "Unknown",
                        DocumentType = "Backfill",
                        ChunksIndexed = chunks.Count,
                        UploadedAt = DateTime.UtcNow
                    });

                    await _db.SaveChangesAsync(cancellationToken);
                    existingBlobPaths.Add(blobPath);
                    processed++;
                }
                catch (NotSupportedException)
                {
                    skipped++;
                    _logger.LogWarning("Skipping unsupported blob type for '{BlobPath}'.", blobPath);
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogError(ex, "Failed to sync blob '{BlobPath}'.", blobPath);
                }
            }

            return Ok(new
            {
                success = true,
                message = $"Sync completed. Processed: {processed}, Skipped: {skipped}, Failed: {failed}.",
                total = candidates.Count,
                processed,
                skipped,
                failed
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync existing blobs from Azure container.");
            return StatusCode(500, new
            {
                success = false,
                message = "An error occurred while syncing existing blobs."
            });
        }
    }

    private string? ValidateFile(IFormFile file)
    {
        if (file.Length == 0)
            return "File is empty.";

        if (file.Length > _blobOptions.MaxFileSizeBytes)
            return $"File exceeds maximum size of {_blobOptions.MaxFileSizeBytes / 1_048_576} MB.";

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!_blobOptions.AllowedExtensions.Contains(extension))
            return $"File type '{extension}' is not allowed. Allowed types: {string.Join(", ", _blobOptions.AllowedExtensions)}";

        return null;
    }

    private bool IsAllowedBlobPath(string blobPath)
    {
        var extension = Path.GetExtension(blobPath).ToLowerInvariant();
        return _blobOptions.AllowedExtensions.Contains(extension);
    }
}

