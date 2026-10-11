using EnterpriseAIBot.Configuration;
using EnterpriseAIBot.Models;
using EnterpriseAIBot.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace EnterpriseAIBot.AI.SemanticKernel;

/// <summary>
/// Wraps Microsoft Semantic Kernel for chat completion.
/// Can be used as an alternative to AzureOpenAIService for SK-native features
/// such as function calling, planners, and memory connectors.
/// </summary>
public sealed class SemanticKernelService
{
    private readonly IAzureOpenAIService _openAIService;
    private readonly AzureOpenAIOptions _options;
    private readonly ILogger<SemanticKernelService> _logger;
    private readonly object _sync = new();

    private Kernel? _kernel;
    private IChatCompletionService? _chatCompletion;
    private bool _kernelInitializationAttempted;

    public SemanticKernelService(
        IOptions<AzureOpenAIOptions> options,
        IAzureOpenAIService openAIService,
        ILogger<SemanticKernelService> logger)
    {
        _options = options.Value;
        _openAIService = openAIService;
        _logger = logger;
    }

    public async Task<string> GetChatCompletionAsync(
        string systemPrompt,
        string userPrompt,
        IEnumerable<ConversationMessage> history,
        CancellationToken ct = default)
    {
        EnsureKernelInitialized();

        if (_kernel is null || _chatCompletion is null)
        {
            _logger.LogWarning("Semantic Kernel Azure connector unavailable. Falling back to AzureOpenAIService.");
            return await _openAIService.GetChatCompletionAsync(systemPrompt, userPrompt, history, ct);
        }

        var chatHistory = new ChatHistory(systemPrompt);

        foreach (var msg in history)
        {
            if (msg.Role == "assistant")
                chatHistory.AddAssistantMessage(msg.Content);
            else
                chatHistory.AddUserMessage(msg.Content);
        }

        chatHistory.AddUserMessage(userPrompt);

        _logger.LogInformation("Semantic Kernel sending chat request. History turns: {Count}", chatHistory.Count);

        var result = await _chatCompletion.GetChatMessageContentAsync(
            chatHistory,
            kernel: _kernel,
            cancellationToken: ct);

        return result.Content ?? string.Empty;
    }

    private void EnsureKernelInitialized()
    {
        if (_kernelInitializationAttempted)
            return;

        lock (_sync)
        {
            if (_kernelInitializationAttempted)
                return;

            _kernelInitializationAttempted = true;

            try
            {
                _kernel = Kernel.CreateBuilder()
                    .AddAzureOpenAIChatCompletion(
                        deploymentName: _options.DeploymentName,
                        endpoint: _options.Endpoint,
                        apiKey: _options.ApiKey)
                    .Build();

                _chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
                _logger.LogInformation("Semantic Kernel Azure chat completion initialized.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Semantic Kernel initialization failed. AzureOpenAIService fallback will be used.");
                _kernel = null;
                _chatCompletion = null;
            }
        }
    }
}

