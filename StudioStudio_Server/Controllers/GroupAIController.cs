using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers;

/// <summary>
/// Controller for Group AI (AI Nhóm)
/// Route: /api/ai/group
/// Tools: get_tasks, get_group_stats, get_members, get_deadlines, search_documents
/// </summary>
[Route("api/ai/group")]
[ApiController]
[Authorize]
public class GroupAIController : ControllerBase
{
    private readonly AIAgent _aiAgent;
    private readonly IGroupParticipantRepository _participantRepository;
    private readonly IAIRequestLogRepository _aiRequestLogRepository;
    private readonly IUserSubscriptionRepository _userSubscriptionRepository;
    private readonly ILogger<GroupAIController> _logger;

    public GroupAIController(
        AIAgent aiAgent,
        IGroupParticipantRepository participantRepository,
        IAIRequestLogRepository aiRequestLogRepository,
        IUserSubscriptionRepository userSubscriptionRepository,
        ILogger<GroupAIController> logger)
    {
        _aiAgent = aiAgent;
        _participantRepository = participantRepository;
        _aiRequestLogRepository = aiRequestLogRepository;
        _userSubscriptionRepository = userSubscriptionRepository;
        _logger = logger;
    }

    /// <summary>
    /// Ask Group AI - AI hỗ trợ nhóm học tập
    /// </summary>
    [HttpPost("ask")]
    public async Task<ActionResult<AIResponse>> AskGroupAI(
        [FromBody] GroupAIRequest request,
        [FromHeader(Name = "Accept-Language")] string language = "vi",
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status401Unauthorized);
        }

        // Validate: User phải là thành viên của nhóm
        var isMember = await _participantRepository.IsUserInGroupAsync(request.GroupId, userId.Value);
        if (!isMember)
        {
            throw new AppException(ErrorCodes.GroupPermissionDenied, StatusCodes.Status403Forbidden);
        }

        // Check rate limit
        var rateLimitResult = await CheckRateLimitAsync(userId.Value);
        if (!rateLimitResult.Allowed)
        {
            throw new AppException(ErrorCodes.AIRateLimitExceeded, StatusCodes.Status429TooManyRequests);
        }

        _logger.LogInformation(
            "Group AI Question: UserId={UserId}, GroupId={GroupId}, Question={Question}",
            userId, request.GroupId,
            request.Question.Length > 100 ? request.Question[..100] + "..." : request.Question);

        try
        {
            // Build context for Group AI
            var context = new AIQueryContext
            {
                UserId = userId.Value,
                Language = language,
                GroupId = request.GroupId
            };

            // Process với AIAgent
            var result = await _aiAgent.ProcessAsync(request.Question, context, cancellationToken);

            // Log AI request (1 per user prompt, regardless of internal tool calls)
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
            _logger.LogError(ex, "Group AI error");
            throw new AppException(
                ErrorCodes.UnexpectedError,
                StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Ask Group AI (Streaming) - Trả lời theo stream
    /// </summary>
    [HttpPost("ask/stream")]
    public async Task AskGroupAIStream(
        [FromBody] GroupAIRequest request,
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

        // Validate membership
        var isMember = await _participantRepository.IsUserInGroupAsync(request.GroupId, userId.Value);
        if (!isMember)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            await Response.WriteAsync("Forbidden: Not a group member");
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
            "Group AI Stream: UserId={UserId}, GroupId={GroupId}",
            userId, request.GroupId);

        try
        {
            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";

            var context = new AIQueryContext
            {
                UserId = userId.Value,
                Language = language,
                GroupId = request.GroupId
            };

            var result = await _aiAgent.ProcessAsync(request.Question, context, cancellationToken);

            // Log AI request (1 per user prompt)
            await LogAIRequestAsync(userId.Value, 1);

            // Send metadata first
            await SendSSEvent(new
            {
                type = "metadata",
                remainingRequests = rateLimitResult.RemainingRequests - 1,
                dailyLimit = rateLimitResult.DailyLimit,
                toolCount = result.ToolCallCount
            });

            // Send answer
            await SendSSEvent(new { type = "chunk", content = result.Answer });
            await SendSSEvent(new { type = "done" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Group AI stream error");
            await SendSSEvent(new { type = "error", message = ex.Message });
        }
        finally
        {
            await Response.CompleteAsync();
        }
    }

    /// <summary>
    /// Get Group AI Info - Lấy thông tin về Group AI
    /// </summary>
    [HttpGet("info/{groupId}")]
    public async Task<ActionResult<AIResponse>> GetGroupAIInfo(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status401Unauthorized);
        }

        // Validate membership
        var isMember = await _participantRepository.IsUserInGroupAsync(groupId, userId.Value);
        if (!isMember)
        {
            throw new AppException(ErrorCodes.GroupPermissionDenied, StatusCodes.Status403Forbidden);
        }

        var rateLimitResult = await CheckRateLimitAsync(userId.Value);

        return Ok(new AIResponse
        {
            Success = true,
            Data = new
            {
                GroupId = groupId,
                AIType = "Group AI",
                Description = "Trợ lý AI hỗ trợ nhóm học tập",
                Capabilities = new[]
                {
                    "Trả lời câu hỏi về công việc nhóm",
                    "Tổng hợp thống kê tiến độ",
                    "Gợi ý deadline",
                    "Phân tích hiệu suất thành viên"
                },
                RateLimit = new
                {
                    RemainingRequests = rateLimitResult.RemainingRequests,
                    DailyLimit = rateLimitResult.DailyLimit,
                    Plan = rateLimitResult.Plan
                }
            }
        });
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
                TokenUsed = toolCallCount * 100, // Estimate
                CreatedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log AI request for UserId={UserId}", userId);
        }
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private async Task SendSSEvent(object data)
    {
        await Response.WriteAsync($"data: {Newtonsoft.Json.JsonConvert.SerializeObject(data)}\n\n");
    }

    /// <summary>
    /// Get AI Suggestions - Proactive suggestions for group improvement
    /// </summary>
    [HttpGet("suggestions/{groupId}")]
    public async Task<ActionResult<AIResponse>> GetGroupSuggestions(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status401Unauthorized);
        }

        var isMember = await _participantRepository.IsUserInGroupAsync(groupId, userId.Value);
        if (!isMember)
        {
            throw new AppException(ErrorCodes.GroupPermissionDenied, StatusCodes.Status403Forbidden);
        }

        var rateLimitResult = await CheckRateLimitAsync(userId.Value);
        if (!rateLimitResult.Allowed)
        {
            throw new AppException(ErrorCodes.AIRateLimitExceeded, StatusCodes.Status429TooManyRequests);
        }

        var context = new AIQueryContext
        {
            UserId = userId.Value,
            Language = "vi",
            GroupId = groupId
        };

        var prompt = "Phân tích dữ liệu nhóm này và đưa ra 3-5 gợi ý cải thiện cho nhóm. "
            + "Ví dụ: công việc chậm tiến độ, deadline sắp tới, thành viên không hoạt động, "
            + "cơ hội cải thiện. Trả lời bằng tiếng Việt.";

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
                SuggestionType = "GroupImprovement"
            },
            Message = result.Success ? "Success" : result.ErrorMessage
        });
    }
}

public class RateLimitResult
{
    public bool Allowed { get; set; }
    public int RemainingRequests { get; set; }
    public int DailyLimit { get; set; }
    public string Plan { get; set; } = "Free";
}

/// <summary>
/// Request model cho Group AI
/// </summary>
public class GroupAIRequest
{
    public Guid GroupId { get; set; }
    public string Question { get; set; } = "";
}
