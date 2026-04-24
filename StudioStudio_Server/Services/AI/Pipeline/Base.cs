using Microsoft.Extensions.Options;
using StudioStudio_Server.Configurations;
using StudioStudio_Server.Services.AI.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using System.Text.Json.Nodes;

namespace StudioStudio_Server.Services.AI.Pipeline;

/// <summary>
/// Base implementation for AIAgent pipeline.
/// Root namespace exposes a thin wrapper that inherits this implementation.
/// </summary>
public partial class AIAgent
{
    private const int HardMaxToolCalls = 5;
    private const int MaxConsecutiveDecideWithoutExecution = 3;
    private const int MaxToolResultCharsForPrompt = 7000;
    private const int MaxArrayItemsForPrompt = 20;
    private const int MaxStringCharsForPrompt = 300;

    private readonly IAIToolRegistry _toolRegistry;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILLMService _llmService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<AIAgent> _logger;
    private readonly AIAgentConfig _config;

    private readonly string _systemPromptVi;
    private readonly string _systemPromptEn;
    private readonly string _personalSystemPromptVi;
    private readonly string _personalSystemPromptEn;
    private readonly string _ownerSystemPromptVi;
    private readonly string _ownerSystemPromptEn;

    private TokenUsage? _currentTokenUsage;

    protected sealed record AIIntentAnalysis(
        string Category,
        bool RequiresTool,
        bool IsTaskIntent,
        bool IsDocumentIntent,
        bool IsFollowUp,
        string Summary);

    protected sealed record AIFlowDecision(
        string StepName,
        AgentDecision Decision,
        JsonObject ToolParameters,
        bool IsAccepted = true,
        string ReviewState = "accepted",
        string? ReviewNote = null,
        string? SuggestedToolName = null,
        JsonObject? SuggestedParameters = null);

    protected sealed record AIReviewVerdict(
        bool IsAccepted,
        string ReviewNote,
        string ReviewState,
        string? SuggestedToolName = null,
        JsonObject? SuggestedParameters = null);

    public AIAgent(
        IAIToolRegistry toolRegistry,
        IServiceProvider serviceProvider,
        ILLMService llmService,
        ICacheService cacheService,
        ILogger<AIAgent> logger,
        IOptions<AIAgentConfig> configOptions)
    {
        _toolRegistry = toolRegistry;
        _serviceProvider = serviceProvider;
        _llmService = llmService;
        _cacheService = cacheService;
        _logger = logger;
        _config = configOptions.Value;

        _systemPromptVi = GetSystemPromptVi();
        _systemPromptEn = GetSystemPromptEn();
        _personalSystemPromptVi = GetPersonalSystemPromptVi();
        _personalSystemPromptEn = GetPersonalSystemPromptEn();
        _ownerSystemPromptVi = GetOwnerSystemPromptVi();
        _ownerSystemPromptEn = GetOwnerSystemPromptEn();
    }

    private int GetEffectiveMaxToolCalls() => Math.Min(_config.MaxToolCalls, HardMaxToolCalls);
}
