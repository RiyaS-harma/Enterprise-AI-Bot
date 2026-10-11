namespace EnterpriseAIBot.Models;

public sealed class SourceDocument
{
    public string DocumentName { get; init; } = string.Empty;
    public int? PageNumber { get; init; }
    public string Department { get; init; } = string.Empty;
    public string DocumentType { get; init; } = string.Empty;
    public double RelevanceScore { get; init; }
}

