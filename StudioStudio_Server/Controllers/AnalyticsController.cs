using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    [Route("api/analytics")]
    [ApiController]
    [Authorize]
    public class AnalyticsController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;
        private readonly IMessageService _messageService;

        public AnalyticsController(
            IAnalyticsService analyticsService,
            IMessageService messageService)
        {
            _analyticsService = analyticsService;
            _messageService = messageService;
        }

        /// <summary>
        /// Authenticate and get userId from JWT token
        /// Validate: User must not be admin (admin cannot use user APIs)
        /// </summary>
        private Guid ValidateAndGetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(
                    ErrorCodes.AuthInvalidCredential,
                    StatusCodes.Status401Unauthorized);
            }

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null &&
                          bool.TryParse(isAdminClaim, out var adminResult) &&
                          adminResult;

            if (isAdmin)
            {
                throw new AppException(
                    ErrorCodes.AuthForbidden,
                    StatusCodes.Status403Forbidden);
            }

            return userId;
        }

        // ==================== USER ANALYTICS ====================

        /// <summary>
        /// Get user dashboard with productivity score, activity heatmap, task completion trend, and deadline performance
        /// </summary>
        [HttpGet("user/dashboard")]
        public async Task<ActionResult<ApiResponse<UserDashboardResponse>>> GetUserDashboard(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var userId = ValidateAndGetUserId();

            DateOnly? start = startDate.HasValue ? DateOnly.FromDateTime(startDate.Value) : null;
            DateOnly? end = endDate.HasValue ? DateOnly.FromDateTime(endDate.Value) : null;

            var result = await _analyticsService.GetUserDashboardAsync(userId, start, end);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<UserDashboardResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        /// <summary>
        /// Get user activity heatmap data
        /// </summary>
        [HttpGet("user/heatmap")]
        public async Task<ActionResult<ApiResponse<List<ActivityHeatmapData>>>> GetUserActivityHeatmap(
            [FromQuery] int days = 30)
        {
            var userId = ValidateAndGetUserId();

            var result = await _analyticsService.GetUserActivityHeatmapAsync(userId, days);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<List<ActivityHeatmapData>>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        /// <summary>
        /// Get task completion trend over time
        /// </summary>
        [HttpGet("user/trends")]
        public async Task<ActionResult<ApiResponse<List<TaskCompletionTrendData>>>> GetTaskCompletionTrend(
            [FromQuery] int days = 30)
        {
            var userId = ValidateAndGetUserId();

            var result = await _analyticsService.GetTaskCompletionTrendAsync(userId, days);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<List<TaskCompletionTrendData>>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        /// <summary>
        /// Get deadline performance (on-time vs late completion)
        /// </summary>
        [HttpGet("user/deadline-performance")]
        public async Task<ActionResult<ApiResponse<DeadlinePerformanceData>>> GetDeadlinePerformance()
        {
            var userId = ValidateAndGetUserId();

            var result = await _analyticsService.GetDeadlinePerformanceAsync(userId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<DeadlinePerformanceData>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        // ==================== GROUP ANALYTICS ====================

        /// <summary>
        /// Get group analytics dashboard
        /// </summary>
        [HttpGet("group/{groupId}")]
        public async Task<ActionResult<ApiResponse<GroupAnalyticsResponse>>> GetGroupAnalytics(
            Guid groupId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var userId = ValidateAndGetUserId();

            DateOnly? start = startDate.HasValue ? DateOnly.FromDateTime(startDate.Value) : null;
            DateOnly? end = endDate.HasValue ? DateOnly.FromDateTime(endDate.Value) : null;

            var result = await _analyticsService.GetGroupAnalyticsAsync(groupId, userId, start, end);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<GroupAnalyticsResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        /// <summary>
        /// Get group member contributions
        /// </summary>
        [HttpGet("group/{groupId}/members")]
        public async Task<ActionResult<ApiResponse<List<MemberContributionData>>>> GetGroupMemberContribution(
            Guid groupId)
        {
            var userId = ValidateAndGetUserId();

            var result = await _analyticsService.GetGroupMemberContributionAsync(groupId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<List<MemberContributionData>>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        // ==================== STUDIO ANALYTICS ====================

        /// <summary>
        /// Get studio group comparison
        /// </summary>
        [HttpGet("studio/{studioId}/groups")]
        public async Task<ActionResult<ApiResponse<List<GroupComparisonData>>>> GetStudioGroupComparison(
            Guid studioId)
        {
            var userId = ValidateAndGetUserId();

            var result = await _analyticsService.GetStudioGroupComparisonAsync(studioId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<List<GroupComparisonData>>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        /// <summary>
        /// Get studio group activity heatmap for chart visualization
        /// Date range: default 30 days (matching UI navigation)
        /// </summary>
        [HttpGet("studio/{studioId}/heatmap")]
        public async Task<ActionResult<ApiResponse<StudioGroupHeatmapResponse>>> GetStudioGroupHeatmap(
            Guid studioId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var userId = ValidateAndGetUserId();

            // Default: endDate = today, startDate = today - 29 days (total 30 days)
            var end = endDate.HasValue ? DateOnly.FromDateTime(endDate.Value) : DateOnly.FromDateTime(DateTime.UtcNow);
            var start = startDate.HasValue ? DateOnly.FromDateTime(startDate.Value) : end.AddDays(-29);

            var result = await _analyticsService.GetStudioGroupHeatmapAsync(studioId, start, end);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<StudioGroupHeatmapResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }
    }
}
