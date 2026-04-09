using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers;

/// <summary>
/// Controller for Personal AI (AI Cá Nhân)
/// Route: /api/ai/personal
/// Tools: get_personal_tasks, get_personal_stats, get_personal_deadlines
/// </summary>
[Route("api/ai/personal")]
[ApiController]
[Authorize]
public class PersonalAIController : ControllerBase
{
    private readonly AIAgent _aiAgent;
    private readonly IUserService _userService;
    private readonly IAIRequestLogRepository _aiRequestLogRepository;
    private readonly IUserSubscriptionRepository _userSubscriptionRepository;
    private readonly ILogger<PersonalAIController> _logger;

    public PersonalAIController(
        AIAgent aiAgent,
        IUserService userService,
        IAIRequestLogRepository aiRequestLogRepository,
        IUserSubscriptionRepository userSubscriptionRepository,
        ILogger<PersonalAIController> logger)
    {
        _aiAgent = aiAgent;
        _userService = userService;
        _aiRequestLogRepository = aiRequestLogRepository;
        _userSubscriptionRepository = userSubscriptionRepository;
        _logger = logger;
    }

    /// <summary>
    /// Ask Personal AI - AI cá nhân về công việc và tiến độ của mình
    /// </summary>
    [HttpPost("ask")]
    public async Task<ActionResult<AIResponse>> AskPersonalAI(
        [FromBody] PersonalAIRequest request,
        [FromHeader(Name = "Accept-Language")] string language = "vi",
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status401Unauthorized);
        }

        // Check rate limit
        var rateLimitResult = await CheckRateLimitAsync(userId.Value);
        if (!rateLimitResult.Allowed)
        {
            throw new AppException(ErrorCodes.AIRateLimitExceeded, StatusCodes.Status429TooManyRequests);
        }

        _logger.LogInformation(
            "Personal AI Question: UserId={UserId}, Question={Question}, Language={Language}",
            userId, request.Question.Length > 100 ? request.Question[..100] + "..." : request.Question, language);

        try
        {
            // Build context for personal AI
            var context = new AIQueryContext
            {
                UserId = userId.Value,
                Language = language,
                // Personal AI không có group_id - chỉ hoạt động trong phạm vi cá nhân
            };

            // Process với AIAgent
            var result = await _aiAgent.ProcessAsync(request.Question, context, cancellationToken);

            // Log AI request with actual token usage from Gemini
            var tokenUsage = result.TokenUsage;
            await LogAIRequestAsync(
                userId.Value,
                result.ToolCallCount,
                tokenUsage?.InputTokens ?? 0,
                tokenUsage?.OutputTokens ?? 0,
                tokenUsage?.CachedTokens ?? 0,
                tokenUsage?.ThinkingTokens ?? 0,
                result.ProcessingTimeMs);

            return Ok(new AIResponse
            {
                Success = result.Success,
                Answer = result.Answer,
                Data = new
                {
                    result.ToolCallCount,
                    result.ProcessingTimeMs,
                    ReasoningSteps = result.ReasoningSteps,
                    RemainingRequests = rateLimitResult.RemainingRequests,
                    DailyLimit = rateLimitResult.DailyLimit
                },
                Message = result.Success ? "Success" : result.ErrorMessage
            });
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Personal AI error");
            throw new AppException(
                ErrorCodes.UnexpectedError,
                StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Ask Personal AI (Streaming) - Trả lời theo stream với progressive display
    /// Sử dụng ProcessStreamAsync để stream từng phần của LLM response
    /// </summary>
    [HttpPost("ask/stream")]
    public async Task AskPersonalAIStream(
        [FromBody] PersonalAIRequest request,
        [FromHeader(Name = "Accept-Language")] string language = "vi",
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Response.WriteAsync("Unauthorized");
            await Response.Body.FlushAsync();
            return;
        }

        // Check rate limit
        var rateLimitResult = await CheckRateLimitAsync(userId.Value);
        if (!rateLimitResult.Allowed)
        {
            Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await Response.WriteAsync("Rate limit exceeded");
            await Response.Body.FlushAsync();
            return;
        }

        _logger.LogInformation(
            "Personal AI Stream: UserId={UserId}, Question={Question}",
            userId, request.Question.Length > 100 ? request.Question[..100] + "..." : request.Question);

        // Token usage will be extracted from metadata chunk after processing
        int toolCount = 0;
        long processingTimeMs = 0;
        int inputTokens = 0, outputTokens = 0, cachedTokens = 0, thinkingTokens = 0;

        try
        {
            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Connection = "keep-alive";

            var context = new AIQueryContext
            {
                UserId = userId.Value,
                Language = language
            };

            // Stream chunks from AIAgent
            await foreach (var chunk in _aiAgent.ProcessStreamAsync(request.Question, context, cancellationToken))
            {
                switch (chunk.Type)
                {
                    case "metadata":
                        // Extract token usage from metadata for logging
                        toolCount = chunk.ToolCount ?? 0;
                        processingTimeMs = chunk.ProcessingTimeMs ?? 0;
                        inputTokens = chunk.InputTokens ?? 0;
                        outputTokens = chunk.OutputTokens ?? 0;
                        cachedTokens = chunk.CachedTokens ?? 0;
                        thinkingTokens = chunk.ThinkingTokens ?? 0;

                        var metadata = JsonConvert.SerializeObject(new
                        {
                            type = "metadata",
                            remainingRequests = rateLimitResult.RemainingRequests - 1,
                            dailyLimit = rateLimitResult.DailyLimit,
                            toolCount = chunk.ToolCount,
                            processingTime = chunk.ProcessingTimeMs,
                            inputTokens = chunk.InputTokens,
                            outputTokens = chunk.OutputTokens,
                            cachedTokens = chunk.CachedTokens,
                            thinkingTokens = chunk.ThinkingTokens
                        });
                        await Response.WriteAsync($"data: {metadata}\n\n");
                        await Response.Body.FlushAsync();
                        break;

                    case "chunk":
                        if (!string.IsNullOrWhiteSpace(chunk.Content))
                        {
                            var chunkData = JsonConvert.SerializeObject(new { type = "chunk", content = chunk.Content });
                            await Response.WriteAsync($"data: {chunkData}\n\n");
                            await Response.Body.FlushAsync();
                        }
                        break;

                    case "done":
                        await Response.WriteAsync("data: {\"type\":\"done\"}\n\n");
                        await Response.Body.FlushAsync();
                        break;

                    case "error":
                        var error = JsonConvert.SerializeObject(new { type = "error", message = chunk.ErrorMessage });
                        await Response.WriteAsync($"data: {error}\n\n");
                        await Response.Body.FlushAsync();
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Personal AI stream cancelled by client");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Personal AI stream error");
            var error = JsonConvert.SerializeObject(new { type = "error", message = "Đã xảy ra lỗi khi xử lý yêu cầu. Vui lòng thử lại sau." });
            await Response.WriteAsync($"data: {error}\n\n");
            await Response.Body.FlushAsync();
        }
        finally
        {
            // Log AI request with actual token usage extracted from streaming metadata
            await LogAIRequestAsync(
                userId.Value,
                toolCount,
                inputTokens,
                outputTokens,
                cachedTokens,
                thinkingTokens,
                processingTimeMs);
            await Response.CompleteAsync();
        }
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private async Task<RateLimitResult> CheckRateLimitAsync(Guid userId)
    {
        var todayRequests = await _aiRequestLogRepository.CountTodayRequestsAsync(userId, DateTime.UtcNow.Date);
        var subscription = await _userSubscriptionRepository.GetSubscriptionPlanByUserIdAsync(userId);
        var dailyLimit = subscription?.MaxAiRequestsPerDay ?? 20;

        return new RateLimitResult
        {
            Allowed = todayRequests < dailyLimit,
            RemainingRequests = Math.Max(0, dailyLimit - todayRequests),
            DailyLimit = dailyLimit,
            Plan = subscription?.PlanName ?? "Free"
        };
    }

    private async Task LogAIRequestAsync(Guid userId, int toolCallCount, int inputTokens, int outputTokens, int cachedTokens, int thinkingTokens, long processingTimeMs)
    {
        try
        {
            await _aiRequestLogRepository.AddAsync(new Models.Entities.AIRequestLog
            {
                RequestId = Guid.NewGuid(),
                UserId = userId,
                // Legacy field for backward compatibility (Input + Output + Cached)
                TokenUsed = inputTokens + outputTokens + cachedTokens,
                // New detailed token tracking
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                CachedTokens = cachedTokens,
                ThinkingTokens = thinkingTokens,
                ToolCallCount = toolCallCount,
                ProcessingTimeMs = processingTimeMs,
                AILayer = "Personal",
                CreatedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log AI request for UserId={UserId}", userId);
        }
    }
}

/// <summary>
/// Request model cho Personal AI
/// </summary>
public class PersonalAIRequest
{
    public string Question { get; set; } = "";
}
