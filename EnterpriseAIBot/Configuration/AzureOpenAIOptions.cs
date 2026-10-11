namespace EnterpriseAIBot.Configuration;

public sealed class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";

    public string Endpoint { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string DeploymentName { get; init; } = string.Empty;   // gpt-4o
    public int MaxTokens { get; init; } = 2000;
    public float Temperature { get; init; } = 0.1f;
}

