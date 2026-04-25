using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI;
using StudioStudio_Server.Services.AI.Models;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers;

/// <summary>
/// Controller for Master AI (AI Toàn Studio)
/// Route: /api/ai/master
/// Tools: get_studio_analytics, get_studio_groups, get_studio_health, get_group_comparison, get_risk_groups
/// Chỉ Owner của Studio mới có quyền sử dụng
/// </summary>
[Route("api/ai/master")]
[ApiController]
[Authorize]
public class MasterAIController(
    AIAgent aiAgent,
    IStudioRepository studioRepository,
    IAIRequestLogRepository aiRequestLogRepository,
    IUserSubscriptionRepository userSubscriptionRepository,
    ILogger<MasterAIController> logger) : ControllerBase
{
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
        var studio = await studioRepository.GetByIdAsync(request.StudioId);
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

        logger.LogInformation(
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
                StudioId = request.StudioId,
                StudioOwnerId = userId.Value  // Studio Owner được quyền gọi Group tools với group_id tuỳ ý
            };

            // Process với AIAgent
            var result = await aiAgent.ProcessAsync(request.Question, context, cancellationToken);

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
                    result.ReasoningSteps,
                    rateLimitResult.RemainingRequests,
                    rateLimitResult.DailyLimit
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
            logger.LogError(ex, "Master AI error");
            throw new AppException(
                ErrorCodes.UnexpectedError,
                StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Ask Master AI (Streaming) - Trả lời theo stream với progressive display
    /// Sử dụng ProcessStreamAsync để stream từng phần của LLM response
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
            await Response.WriteAsync("Unauthorized", cancellationToken: cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
            return;
        }

        // Validate Studio Owner
        var studio = await studioRepository.GetByIdAsync(request.StudioId);
        if (studio == null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            await Response.WriteAsync("Studio not found", cancellationToken: cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
            return;
        }

        if (studio.OwnerId != userId.Value)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            await Response.WriteAsync("Forbidden: Only Studio Owner can use Master AI", cancellationToken: cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
            return;
        }

        // Check rate limit
        var rateLimitResult = await CheckRateLimitAsync(userId.Value);
        if (!rateLimitResult.Allowed)
        {
            Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await Response.WriteAsync("Rate limit exceeded", cancellationToken: cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
            return;
        }

        logger.LogInformation(
            "Master AI Stream: UserId={UserId}, StudioId={StudioId}, Question={Question}",
            userId, request.StudioId,
            request.Question.Length > 100 ? request.Question[..100] + "..." : request.Question);

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
                StudioId = request.StudioId,
                StudioOwnerId = userId.Value
            };

            // Stream chunks from AIAgent
            await foreach (var chunk in aiAgent.ProcessStreamAsync(request.Question, context, cancellationToken))
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
            logger.LogInformation("Master AI stream cancelled by client");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Master AI stream error");
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

        var studio = await studioRepository.GetByIdAsync(studioId);
        if (studio == null)
            throw new AppException(ErrorCodes.StudioNotFound, StatusCodes.Status404NotFound);

        if (studio.OwnerId != userId.Value)
            throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);

        var rateLimitResult = await CheckRateLimitAsync(userId.Value);

        // Get groups to count members
        var groups = await studioRepository.GetGroupsByStudioIdAsync(studioId);
        var totalMembers = groups.Sum(g => g.Participants?.Count ?? 0);

        return Ok(new AIResponse
        {
            Success = true,
            Data = new
            {
                StudioId = studioId,
                studio.StudioName,
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
                    rateLimitResult.RemainingRequests,
                    rateLimitResult.DailyLimit,
                    rateLimitResult.Plan
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

        var studio = await studioRepository.GetByIdAsync(studioId);
        if (studio == null)
            throw new AppException(ErrorCodes.StudioNotFound, StatusCodes.Status404NotFound);

        if (studio.OwnerId != userId.Value)
            throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);

        var now = DateTime.UtcNow;
        var startOfDay = now.Date;

        // Get owner's AI usage
        var todayRequests = await aiRequestLogRepository.CountTodayRequestsAsync(userId.Value, startOfDay);
        var todayTokens = await aiRequestLogRepository.GetTodayTokenUsageAsync(userId.Value, startOfDay);

        var subscription = await userSubscriptionRepository.GetSubscriptionPlanByUserIdAsync(userId.Value);
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
        var todayRequests = await aiRequestLogRepository.CountTodayRequestsAsync(userId, DateTime.UtcNow.Date);
        var subscription = await userSubscriptionRepository.GetSubscriptionPlanByUserIdAsync(userId);
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
            await aiRequestLogRepository.AddAsync(new Models.Entities.AIRequestLog
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
                AILayer = "Master",
                CreatedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to log AI request for UserId={UserId}", userId);
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
        await Response.Body.FlushAsync();
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
