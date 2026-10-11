namespace EnterpriseAIBot.Services.Interfaces;

public interface IBlobStorageService
{
    Task<string> UploadAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task<Stream> DownloadAsync(string blobPath, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string blobPath, CancellationToken cancellationToken = default);
    Task<List<string>> ListBlobPathsAsync(CancellationToken cancellationToken = default);
}

