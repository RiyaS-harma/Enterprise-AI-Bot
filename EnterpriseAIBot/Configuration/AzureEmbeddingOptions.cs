namespace EnterpriseAIBot.Configuration;

public sealed class AzureEmbeddingOptions
{
    public const string SectionName = "AzureEmbedding";

    // Shares the same endpoint and API key as AzureOpenAI
    public string DeploymentName { get; init; } = string.Empty;   // text-embedding-3-large
}

