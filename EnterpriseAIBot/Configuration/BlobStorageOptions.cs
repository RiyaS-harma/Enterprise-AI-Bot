namespace EnterpriseAIBot.Configuration;

public sealed class BlobStorageOptions
{
    public const string SectionName = "AzureBlobStorage";

    public string ConnectionString { get; init; } = string.Empty;
    public string ContainerName { get; init; } = string.Empty;
    public long MaxFileSizeBytes { get; init; } = 52_428_800; // 50 MB
    public string[] AllowedExtensions { get; init; } = [".pdf", ".docx", ".txt"];
}

