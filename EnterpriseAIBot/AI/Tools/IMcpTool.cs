namespace EnterpriseAIBot.AI.Tools;

/// <summary>
/// Contract for MCP (Model Context Protocol) tool integrations.
/// Each tool exposes a named capability callable from orchestration services or Semantic Kernel.
/// </summary>
public interface IMcpTool
{
    string ToolName { get; }
    Task<string> ExecuteAsync(Dictionary<string, string> parameters, CancellationToken ct = default);
}

