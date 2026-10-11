using Azure;
using Azure.AI.OpenAI;
using EnterpriseAIBot.Configuration;
using EnterpriseAIBot.Models;
using EnterpriseAIBot.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using OpenAI.Embeddings;

namespace EnterpriseAIBot.Services;

public sealed class AzureOpenAIService : IAzureOpenAIService
{
    private readonly AzureOpenAIClient _client;
    private readonly AzureOpenAIOptions _chatOptions;
    private readonly AzureEmbeddingOptions _embeddingOptions;
    private readonly ILogger<AzureOpenAIService> _logger;

    public AzureOpenAIService(
        IOptions<AzureOpenAIOptions> chatOptions,
        IOptions<AzureEmbeddingOptions> embeddingOptions,
        ILogger<AzureOpenAIService> logger)
    {
        _chatOptions = chatOptions.Value;
        _embeddingOptions = embeddingOptions.Value;
        _logger = logger;
        _client = new AzureOpenAIClient(
            new Uri(_chatOptions.Endpoint),
            new AzureKeyCredential(_chatOptions.ApiKey));
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            var embeddingClient = _client.GetEmbeddingClient(_embeddingOptions.DeploymentName);
            var response = await embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);
            return response.Value.ToFloats().ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding.");
            throw;
        }
    }

    public async Task<string> GetChatCompletionAsync(
        string systemPrompt,
        string userPrompt,
        IEnumerable<ConversationMessage> history,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var chatClient = _client.GetChatClient(_chatOptions.DeploymentName);

            var messages = new List<ChatMessage> { ChatMessage.CreateSystemMessage(systemPrompt) };

            foreach (var msg in history)
            {
                messages.Add(msg.Role == "assistant"
                    ? ChatMessage.CreateAssistantMessage(msg.Content)
                    : ChatMessage.CreateUserMessage(msg.Content));
            }

            messages.Add(ChatMessage.CreateUserMessage(userPrompt));

            var options = new ChatCompletionOptions
            {
                MaxOutputTokenCount = _chatOptions.MaxTokens,
                Temperature = _chatOptions.Temperature
            };

            var response = await chatClient.CompleteChatAsync(messages, options, cancellationToken);
            return response.Value.Content[0].Text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get chat completion.");
            throw;
        }
    }
}

