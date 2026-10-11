namespace EnterpriseAIBot.Models;

public sealed class ChatResponse
{
    public string Answer { get; init; } = string.Empty;
    public List<SourceDocument> Sources { get; init; } = [];
    public bool IsError { get; init; }
    public string? ErrorMessage { get; init; }

    public static ChatResponse Success(string answer, List<SourceDocument> sources) =>
        new() { Answer = answer, Sources = sources };

    public static ChatResponse NotFound() =>
        new() { Answer = "I could not find sufficient information in the available documents to answer your question. Please try rephrasing or contact your HR/IT department directly." };

    public static ChatResponse Error(string userMessage) =>
        new() { IsError = true, ErrorMessage = userMessage, Answer = string.Empty };
}

