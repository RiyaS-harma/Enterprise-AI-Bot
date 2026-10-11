using System.Text;
using EnterpriseAIBot.Models;
using EnterpriseAIBot.Services.Interfaces;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace EnterpriseAIBot.Services;

public sealed class DocumentProcessingService : IDocumentProcessingService
{
    private const int ChunkSize = 500;       // words per chunk
    private const int ChunkOverlap = 50;     // overlapping words between chunks

    private readonly ILogger<DocumentProcessingService> _logger;

    public DocumentProcessingService(ILogger<DocumentProcessingService> logger) => _logger = logger;

    public async Task<List<DocumentChunk>> ProcessDocumentAsync(
        Stream fileStream,
        string fileName,
        string blobPath,
        string department,
        string documentType,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var documentId = Guid.NewGuid().ToString("N");

        _logger.LogInformation("Processing document: {FileName}, type: {Extension}", fileName, extension);

        var pages = extension switch
        {
            ".pdf" => ExtractPdfPages(fileStream),
            ".docx" => ExtractDocxPages(fileStream),
            ".txt" => ExtractTxtPages(fileStream),
            _ => throw new NotSupportedException($"File type '{extension}' is not supported.")
        };

        var chunks = new List<DocumentChunk>();
        int chunkIndex = 0;

        foreach (var (text, pageNumber) in pages)
        {
            var pageChunks = SplitIntoChunks(text);
            foreach (var chunkText in pageChunks)
            {
                chunks.Add(new DocumentChunk
                {
                    Id = $"{documentId}_{chunkIndex}",
                    DocumentId = documentId,
                    DocumentName = fileName,
                    Content = chunkText,
                    ChunkId = $"chunk_{chunkIndex}",
                    PageNumber = pageNumber,
                    BlobPath = blobPath,
                    Department = department,
                    DocumentType = documentType,
                    UploadDate = DateTimeOffset.UtcNow
                });
                chunkIndex++;
            }
        }

        _logger.LogInformation("Document '{FileName}' split into {Count} chunks.", fileName, chunks.Count);
        return await Task.FromResult(chunks);
    }

    private static IEnumerable<(string Text, int? PageNumber)> ExtractPdfPages(Stream stream)
    {
        using var pdf = PdfDocument.Open(stream);
        foreach (var page in pdf.GetPages())
        {
            var text = string.Join(" ", page.GetWords().Select(w => w.Text));
            if (!string.IsNullOrWhiteSpace(text))
                yield return (text, page.Number);
        }
    }

    private static IEnumerable<(string Text, int? PageNumber)> ExtractDocxPages(Stream stream)
    {
        using var doc = WordprocessingDocument.Open(stream, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) yield break;

        var sb = new StringBuilder();
        foreach (var para in body.Descendants<Paragraph>())
        {
            var text = para.InnerText;
            if (!string.IsNullOrWhiteSpace(text))
                sb.AppendLine(text);
        }
        yield return (sb.ToString(), null);
    }

    private static IEnumerable<(string Text, int? PageNumber)> ExtractTxtPages(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var text = reader.ReadToEnd();
        if (!string.IsNullOrWhiteSpace(text))
            yield return (text, null);
    }

    /// <summary>
    /// Splits text into overlapping word-based chunks (LangChain-style RecursiveCharacterTextSplitter equivalent).
    /// </summary>
    private static List<string> SplitIntoChunks(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();

        for (int i = 0; i < words.Length; i += ChunkSize - ChunkOverlap)
        {
            var chunk = string.Join(" ", words.Skip(i).Take(ChunkSize));
            if (!string.IsNullOrWhiteSpace(chunk))
                chunks.Add(chunk);

            if (i + ChunkSize >= words.Length) break;
        }

        return chunks;
    }
}

