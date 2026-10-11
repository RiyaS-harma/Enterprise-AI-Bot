namespace EnterpriseAIBot.Configuration;

public sealed class AzureSearchOptions
{
    public const string SectionName = "AzureSearch";

    public string Endpoint { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string IndexName { get; init; } = string.Empty;
    public int TopK { get; init; } = 8;
    public double RelevanceThreshold { get; init; } = 0.25;
}

