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

            // Log AI request (1 per user prompt, regardless of tool calls)
            await LogAIRequestAsync(userId.Value, 1);

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
    /// Ask Personal AI (Streaming) - Trả lời theo stream
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
            return;
        }

        // Check rate limit
        var rateLimitResult = await CheckRateLimitAsync(userId.Value);
        if (!rateLimitResult.Allowed)
        {
            Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await Response.WriteAsync("Rate limit exceeded");
            return;
        }

        _logger.LogInformation(
            "Personal AI Stream: UserId={UserId}, Question={Question}",
            userId, request.Question.Length > 100 ? request.Question[..100] + "..." : request.Question);

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

            var result = await _aiAgent.ProcessAsync(request.Question, context, cancellationToken);

            // Log AI request (1 per user prompt)
            await LogAIRequestAsync(userId.Value, 1);

            // Send metadata first
            var metadata = JsonConvert.SerializeObject(new
            {
                type = "metadata",
                remainingRequests = rateLimitResult.RemainingRequests - 1,
                dailyLimit = rateLimitResult.DailyLimit,
                toolCount = result.ToolCallCount,
                processingTime = result.ProcessingTimeMs
            });
            await Response.WriteAsync($"data: {metadata}\n\n");

            // Send answer
            var chunk = JsonConvert.SerializeObject(new { type = "chunk", content = result.Answer });
            await Response.WriteAsync($"data: {chunk}\n\n");
            await Response.WriteAsync("data: {\"type\":\"done\"}\n\n");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Personal AI stream error");
            var error = JsonConvert.SerializeObject(new { type = "error", message = "Đã xảy ra lỗi khi xử lý yêu cầu." });
            await Response.WriteAsync($"data: {error}\n\n");
        }
        finally
        {
            await Response.CompleteAsync();
        }
    }

    /// <summary>
    /// Get AI Suggestions - Proactive suggestions for personal productivity
    /// </summary>
    [HttpGet("suggestions")]
    public async Task<ActionResult<AIResponse>> GetPersonalSuggestions(
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status401Unauthorized);
        }

        var rateLimitResult = await CheckRateLimitAsync(userId.Value);
        if (!rateLimitResult.Allowed)
        {
            throw new AppException(ErrorCodes.AIRateLimitExceeded, StatusCodes.Status429TooManyRequests);
        }

        var context = new AIQueryContext
        {
            UserId = userId.Value,
            Language = "vi"
        };

        var prompt = "Phân tích công việc và tiến độ của tôi, đưa ra 3-5 gợi ý "
            + "để cải thiện năng suất cá nhân. Ví dụ: công việc quá hạn, deadline sắp tới, "
            + "cách sắp xếp thời gian hiệu quả hơn. Trả lời bằng tiếng Việt.";

        var result = await _aiAgent.ProcessAsync(prompt, context, cancellationToken);
        await LogAIRequestAsync(userId.Value, 1);

        return Ok(new AIResponse
        {
            Success = result.Success,
            Answer = result.Answer,
            Data = new
            {
                result.ToolCallCount,
                result.ProcessingTimeMs,
                ReasoningSteps = result.ReasoningSteps,
                RemainingRequests = rateLimitResult.RemainingRequests - 1,
                DailyLimit = rateLimitResult.DailyLimit,
                SuggestionType = "PersonalProductivity"
            },
            Message = result.Success ? "Success" : result.ErrorMessage
        });
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

    private async Task LogAIRequestAsync(Guid userId, int toolCallCount)
    {
        try
        {
            await _aiRequestLogRepository.AddAsync(new Models.Entities.AIRequestLog
            {
                RequestId = Guid.NewGuid(),
                UserId = userId,
                TokenUsed = toolCallCount * 100, // Estimate: 1 per user prompt
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
    public Guid? PersonalGroupId { get; set; } // Optional: nếu muốn hỏi về personal group
}
