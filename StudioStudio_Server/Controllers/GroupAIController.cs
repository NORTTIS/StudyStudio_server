using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.Enums;
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

        // Validate: phải là thành viên, và không phải Viewer/Commenter
        if (!await _participantRepository.IsUserInGroupAsync(request.GroupId, userId.Value))
        {
            throw new AppException(ErrorCodes.GroupPermissionDenied, StatusCodes.Status403Forbidden);
        }
        var role = await _participantRepository.GetGroupRoleByUserIdAsync(userId.Value, request.GroupId);
        if (role == GroupRole.Viewer || role == GroupRole.Commenter)
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
            string.IsNullOrEmpty(request.Question)
                ? "null"
                : (request.Question.Length > 100 ? request.Question[..100] + "..." : request.Question));

        try
        {
            // Build context for Group AI
            var context = new AIQueryContext
            {
                UserId = userId.Value,
                Language = language,
                GroupId = request.GroupId,
                SessionId = request.SessionId
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
            _logger.LogError(ex, "Group AI error");
            throw new AppException(
                ErrorCodes.UnexpectedError,
                StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Ask Group AI (Streaming) - Trả lời theo stream với progressive display
    /// Sử dụng ProcessStreamAsync để stream từng phần của LLM response
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
            await Response.Body.FlushAsync();
            return;
        }

        // Validate: phải là thành viên, và không phải Viewer/Commenter
        if (!await _participantRepository.IsUserInGroupAsync(request.GroupId, userId.Value))
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            await Response.WriteAsync("Forbidden: Not a group member");
            await Response.Body.FlushAsync();
            return;
        }
        var streamRole = await _participantRepository.GetGroupRoleByUserIdAsync(userId.Value, request.GroupId);
        if (streamRole == GroupRole.Viewer || streamRole == GroupRole.Commenter)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            await Response.WriteAsync("Forbidden: You do not have permission to use Group AI");
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
            "Group AI Stream: UserId={UserId}, GroupId={GroupId}, Question={Question}",
            userId, request.GroupId,
            string.IsNullOrEmpty(request.Question)
                ? "null"
                : (request.Question.Length > 100 ? request.Question[..100] + "..." : request.Question));

        // Token usage will be extracted from metadata chunk after processing
        int toolCount = 0;
        long processingTimeMs = 0;
        int inputTokens = 0, outputTokens = 0, cachedTokens = 0, thinkingTokens = 0;

        try
        {
            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";

            var context = new AIQueryContext
            {
                UserId = userId.Value,
                Language = language,
                GroupId = request.GroupId,
                SessionId = request.SessionId
            };

            // Stream chunks from AIAgent
            await foreach (var chunk in _aiAgent.ProcessStreamAsync(request.Question, context, cancellationToken))
            {
                switch (chunk.Type)
                {
                    case "metadata":
                        toolCount = chunk.ToolCount ?? 0;
                        processingTimeMs = chunk.ProcessingTimeMs ?? 0;
                        inputTokens = chunk.InputTokens ?? 0;
                        outputTokens = chunk.OutputTokens ?? 0;
                        cachedTokens = chunk.CachedTokens ?? 0;
                        thinkingTokens = chunk.ThinkingTokens ?? 0;

                        await SendSSEvent(new
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
                        break;

                    case "chunk":
                        if (!string.IsNullOrWhiteSpace(chunk.Content))
                        {
                            await SendSSEvent(new { type = "chunk", content = chunk.Content });
                        }
                        break;

                    case "done":
                        await SendSSEvent(new { type = "done" });
                        break;

                    case "error":
                        await SendSSEvent(new { type = "error", message = chunk.ErrorMessage });
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Group AI stream cancelled by client");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Group AI stream error");
            await SendSSEvent(new { type = "error", message = "Đã xảy ra lỗi khi xử lý yêu cầu. Vui lòng thử lại sau." });
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

    private async Task LogAIRequestAsync(Guid userId, int toolCallCount, int inputTokens, int outputTokens, int cachedTokens, int thinkingTokens, long processingTimeMs)
    {
        try
        {
            await _aiRequestLogRepository.AddAsync(new Models.Entities.AIRequestLog
            {
                RequestId = Guid.NewGuid(),
                UserId = userId,
                TokenUsed = inputTokens + outputTokens + cachedTokens,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                CachedTokens = cachedTokens,
                ThinkingTokens = thinkingTokens,
                ToolCallCount = toolCallCount,
                ProcessingTimeMs = processingTimeMs,
                AILayer = "Group",
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
        await Response.Body.FlushAsync();
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
    public string? SessionId { get; set; }
}
