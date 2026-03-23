using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
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
/// Controller for Master AI (AI Toàn Studio)
/// Route: /api/ai/master
/// Tools: get_studio_stats, get_all_groups, get_studio_members, get_studio_reports
/// Chỉ Owner của Studio mới có quyền sử dụng
/// </summary>
[Route("api/ai/master")]
[ApiController]
[Authorize]
public class MasterAIController : ControllerBase
{
    private readonly AIAgent _aiAgent;
    private readonly IStudioRepository _studioRepository;
    private readonly IStudioParticipantRepository _studioParticipantRepository;
    private readonly IAIRequestLogRepository _aiRequestLogRepository;
    private readonly IUserSubscriptionRepository _userSubscriptionRepository;
    private readonly ILogger<MasterAIController> _logger;

    public MasterAIController(
        AIAgent aiAgent,
        IStudioRepository studioRepository,
        IStudioParticipantRepository studioParticipantRepository,
        IAIRequestLogRepository aiRequestLogRepository,
        IUserSubscriptionRepository userSubscriptionRepository,
        ILogger<MasterAIController> logger)
    {
        _aiAgent = aiAgent;
        _studioRepository = studioRepository;
        _studioParticipantRepository = studioParticipantRepository;
        _aiRequestLogRepository = aiRequestLogRepository;
        _userSubscriptionRepository = userSubscriptionRepository;
        _logger = logger;
    }

    /// <summary>
    /// Ask Master AI - AI quản lý toàn Studio
    /// Chỉ Owner của Studio mới có quyền sử dụng
    /// </summary>
    [HttpPost("ask")]
    public async Task<ActionResult<AIResponse>> AskMasterAI(
        [FromBody] MasterAIRequest request,
        [FromHeader(Name = "Accept-Language")] string language = "vi",
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status401Unauthorized);
        }

        // Validate: User phải là Owner của Studio
        var studio = await _studioRepository.GetByIdAsync(request.StudioId);
        if (studio == null)
        {
            throw new AppException(ErrorCodes.StudioNotFound, StatusCodes.Status404NotFound);
        }

        var isOwner = studio.OwnerId == userId.Value;
        if (!isOwner)
        {
            throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
        }

        // Check rate limit
        var rateLimitResult = await CheckRateLimitAsync(userId.Value);
        if (!rateLimitResult.Allowed)
        {
            throw new AppException(ErrorCodes.AIRateLimitExceeded, StatusCodes.Status429TooManyRequests);
        }

        _logger.LogInformation(
            "Master AI Question: UserId={UserId}, StudioId={StudioId}, Question={Question}",
            userId, request.StudioId,
            request.Question.Length > 100 ? request.Question[..100] + "..." : request.Question);

        try
        {
            // Build context for Master AI
            var context = new AIQueryContext
            {
                UserId = userId.Value,
                Language = language,
                StudioId = request.StudioId
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
            _logger.LogError(ex, "Master AI error");
            throw new AppException(
                ErrorCodes.UnexpectedError,
                StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Ask Master AI (Streaming) - Trả lời theo stream
    /// </summary>
    [HttpPost("ask/stream")]
    public async Task AskMasterAIStream(
        [FromBody] MasterAIRequest request,
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

        // Validate Studio Owner
        var studio = await _studioRepository.GetByIdAsync(request.StudioId);
        if (studio == null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            await Response.WriteAsync("Studio not found");
            return;
        }

        if (studio.OwnerId != userId.Value)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            await Response.WriteAsync("Forbidden: Only Studio Owner can use Master AI");
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
            "Master AI Stream: UserId={UserId}, StudioId={StudioId}",
            userId, request.StudioId);

        try
        {
            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";

            var context = new AIQueryContext
            {
                UserId = userId.Value,
                Language = language,
                StudioId = request.StudioId
            };

            var result = await _aiAgent.ProcessAsync(request.Question, context, cancellationToken);

            // Log AI request (1 per user prompt)
            await LogAIRequestAsync(userId.Value, 1);

            // Send metadata
            await SendSSEvent(new
            {
                type = "metadata",
                remainingRequests = rateLimitResult.RemainingRequests - 1,
                dailyLimit = rateLimitResult.DailyLimit,
                toolCount = result.ToolCallCount
            });

            // Send full answer as one chunk to avoid losing text due to sentence splitting
            if (!string.IsNullOrWhiteSpace(result.Answer))
            {
                await SendSSEvent(new { type = "chunk", content = result.Answer });
            }
            await SendSSEvent(new { type = "done" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Master AI stream error");
            await SendSSEvent(new { type = "error", message = ex.Message });
        }
        finally
        {
            await Response.CompleteAsync();
        }
    }

    /// <summary>
    /// Get Master AI Info - Lấy thông tin về Master AI
    /// </summary>
    [HttpGet("info/{studioId}")]
    public async Task<ActionResult<AIResponse>> GetMasterAIInfo(
        Guid studioId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == null)
            throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status401Unauthorized);

        var studio = await _studioRepository.GetByIdAsync(studioId);
        if (studio == null)
            throw new AppException(ErrorCodes.StudioNotFound, StatusCodes.Status404NotFound);

        if (studio.OwnerId != userId.Value)
            throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);

        var rateLimitResult = await CheckRateLimitAsync(userId.Value);

        // Get groups to count members
        var groups = await _studioRepository.GetGroupsByStudioIdAsync(studioId);
        var totalMembers = groups.Sum(g => g.Participants?.Count ?? 0);

        return Ok(new AIResponse
        {
            Success = true,
            Data = new
            {
                StudioId = studioId,
                StudioName = studio.StudioName,
                AIType = "Master AI",
                Description = "Trợ lý AI quản lý toàn Studio - chỉ dành cho Owner",
                Capabilities = new[]
                {
                    "Tổng quan thống kê Studio",
                    "Phân tích hiệu suất các nhóm",
                    "Quản lý thành viên Studio",
                    "Báo cáo và insights",
                    "Đề xuất cải thiện"
                },
                Restrictions = new[]
                {
                    "Chỉ Owner mới có quyền sử dụng",
                    "Có quyền truy cập tất cả data trong Studio"
                },
                RateLimit = new
                {
                    RemainingRequests = rateLimitResult.RemainingRequests,
                    DailyLimit = rateLimitResult.DailyLimit,
                    Plan = rateLimitResult.Plan
                },
                StudioStats = new
                {
                    TotalGroups = groups.Count,
                    TotalMembers = totalMembers
                }
            }
        });
    }

    /// <summary>
    /// Get AI Usage Stats - Lấy thống kê sử dụng AI của Owner
    /// </summary>
    [HttpGet("stats/{studioId}")]
    public async Task<ActionResult<AIResponse>> GetAIUsageStats(
        Guid studioId,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId == null)
            throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status401Unauthorized);

        var studio = await _studioRepository.GetByIdAsync(studioId);
        if (studio == null)
            throw new AppException(ErrorCodes.StudioNotFound, StatusCodes.Status404NotFound);

        if (studio.OwnerId != userId.Value)
            throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);

        var now = DateTime.UtcNow;
        var startOfDay = now.Date;

        // Get owner's AI usage
        var todayRequests = await _aiRequestLogRepository.CountTodayRequestsAsync(userId.Value, startOfDay);
        var todayTokens = await _aiRequestLogRepository.GetTodayTokenUsageAsync(userId.Value, startOfDay);

        var subscription = await _userSubscriptionRepository.GetSubscriptionPlanByUserIdAsync(userId.Value);
        var dailyLimit = subscription?.MaxAiRequestsPerDay ?? 20;

        return Ok(new AIResponse
        {
            Success = true,
            Data = new
            {
                StudioId = studioId,
                DateRange = new { Start = startOfDay, End = now },
                Usage = new
                {
                    TotalRequests = todayRequests,
                    TotalTokens = todayTokens,
                    AvgTokensPerRequest = todayRequests > 0 ? todayTokens / todayRequests : 0
                },
                RateLimit = new
                {
                    RemainingRequests = Math.Max(0, dailyLimit - todayRequests),
                    DailyLimit = dailyLimit,
                    Plan = subscription?.PlanName ?? "Free"
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
                TokenUsed = toolCallCount * 100,
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
        await Response.WriteAsync($"data: {JsonConvert.SerializeObject(data)}\n\n");
    }

}

/// <summary>
/// Request model cho Master AI
/// </summary>
public class MasterAIRequest
{
    public Guid StudioId { get; set; }
    public string Question { get; set; } = "";
}
