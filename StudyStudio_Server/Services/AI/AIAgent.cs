using System.Text.Json.Nodes;
using StudioStudio_Server.Services.AI.Pipeline;
using StudioStudio_Server.Services.AI.Models;

namespace StudioStudio_Server.Services.AI;

/// <summary>
/// Public wrapper that preserves the original root namespace type for DI and controllers.
/// </summary>
public class AIAgent : Pipeline.AIAgent
{
    public AIAgent(
        IAIToolRegistry toolRegistry,
        IServiceProvider serviceProvider,
        ILLMService llmService,
        ICacheService cacheService,
        ILogger<AIAgent> logger,
        IOptions<AIAgentConfig> configOptions)
        : base(toolRegistry, serviceProvider, llmService, cacheService, logger, configOptions)
    {
    }
}

/// <summary>
/// Kết quả trả về từ AIAgent
/// </summary>
public class AIAgentResult
{
    public string Answer { get; set; } = "";
    public List<string> ReasoningSteps { get; set; } = new();
    public List<ToolCallEntry> ToolCalls { get; set; } = new();
    public long ProcessingTimeMs { get; set; }
    public int ToolCallCount { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    /// <summary>
    /// Lý do khi fallback fire — null nếu LLM trả lời thật.
    /// </summary>
    public string? FallbackReason { get; set; }
    public TokenUsage? TokenUsage { get; set; }
}

/// <summary>
/// Quyết định của Agent
/// </summary>
public class AgentDecision
{
    public bool ShouldCallTool { get; set; }
    public string? ToolName { get; set; }
    public JsonObject? ToolParameters { get; set; }
    public string? FinalAnswer { get; set; }
}

/// <summary>
/// SSE chunk for streaming AI responses
/// </summary>
public class AIStreamChunk
{
    public string Type { get; set; } = "";
    public string? Content { get; set; }
    public int? RemainingRequests { get; set; }
    public int? DailyLimit { get; set; }
    public int? ToolCount { get; set; }
    public long? ProcessingTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? CachedTokens { get; set; }
    public int? ThinkingTokens { get; set; }
}

internal class AIStreamResult
{
    public int ToolCount { get; set; }
    public long ProcessingTimeMs { get; set; }
    public List<AIStreamChunk> Chunks { get; set; } = new();
    public TokenUsage? TokenUsage { get; set; }
}
