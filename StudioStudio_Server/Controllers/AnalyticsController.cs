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
        /// Get group summary (all time, no date filter)
        /// Returns task breakdown, activity summary, and contribution data
        /// </summary>
        [HttpGet("group/{groupId}/summary")]
        public async Task<ActionResult<ApiResponse<GroupSummaryResponse>>> GetGroupSummary(
            Guid groupId)
        {
            var userId = ValidateAndGetUserId();

            var result = await _analyticsService.GetGroupSummaryAsync(groupId, userId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<GroupSummaryResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        /// <summary>
        /// Get member progress trend with date filter
        /// </summary>
        [HttpGet("group/{groupId}/trend")]
        public async Task<ActionResult<ApiResponse<List<MemberProgressTrendData>>>> GetMemberProgressTrend(
            Guid groupId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var userId = ValidateAndGetUserId();

            DateOnly? start = startDate.HasValue ? DateOnly.FromDateTime(startDate.Value) : null;
            DateOnly? end = endDate.HasValue ? DateOnly.FromDateTime(endDate.Value) : null;

            var result = await _analyticsService.GetMemberProgressTrendAsync(groupId, start, end);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<List<MemberProgressTrendData>>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        /// <summary>
        /// Get member heatmap with date filter
        /// </summary>
        [HttpGet("group/{groupId}/heatmap")]
        public async Task<ActionResult<ApiResponse<List<MemberHeatmapData>>>> GetMemberHeatmap(
            Guid groupId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var userId = ValidateAndGetUserId();

            DateOnly? start = startDate.HasValue ? DateOnly.FromDateTime(startDate.Value) : null;
            DateOnly? end = endDate.HasValue ? DateOnly.FromDateTime(endDate.Value) : null;

            var result = await _analyticsService.GetMemberHeatmapAsync(groupId, start, end);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<List<MemberHeatmapData>>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        /// <summary>
        /// Get group member contributions (kept for backward compatibility)
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

        // ==================== STUDIO OVERVIEW (Chart 1 & 2) ====================

        /// <summary>
        /// GET /api/analytics/studio/{studioId}/overview
        ///
        /// Returns studio overview with all groups summary (no date filter).
        /// Used for Chart 1 (Group Progress) & Chart 2 (Task Status per group).
        /// </summary>
        [HttpGet("studio/{studioId}/overview")]
        public async Task<ActionResult<ApiResponse<StudioOverviewResponse>>> GetStudioOverview(
            Guid studioId)
        {
            var userId = ValidateAndGetUserId();

            var result = await _analyticsService.GetStudioOverviewAsync(studioId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<StudioOverviewResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        // ==================== STUDIO COMPLETION TREND (Chart 3) ====================

        /// <summary>
        /// GET /api/analytics/studio/{studioId}/completion-trend
        ///
        /// Returns completion trend per group WITH date filter.
        /// Used for Chart 3 (Line Chart).
        /// </summary>
        [HttpGet("studio/{studioId}/completion-trend")]
        public async Task<ActionResult<ApiResponse<StudioCompletionTrendResponse>>> GetStudioCompletionTrend(
            Guid studioId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string? groupIds = null)
        {
            var userId = ValidateAndGetUserId();

            DateOnly? start = startDate.HasValue ? DateOnly.FromDateTime(startDate.Value) : null;
            DateOnly? end = endDate.HasValue ? DateOnly.FromDateTime(endDate.Value) : null;

            // Parse comma-separated groupIds
            List<Guid>? parsedGroupIds = null;
            if (!string.IsNullOrWhiteSpace(groupIds))
            {
                parsedGroupIds = groupIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => Guid.TryParse(s.Trim(), out var g) ? g : (Guid?)null)
                    .Where(g => g.HasValue)
                    .Select(g => g!.Value)
                    .ToList();
            }

            var result = await _analyticsService.GetStudioCompletionTrendAsync(studioId, start, end, parsedGroupIds);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<StudioCompletionTrendResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        // ==================== STUDIO GROUP STATUS (Chart 4) ====================

        /// <summary>
        /// GET /api/analytics/studio/{studioId}/group-status
        ///
        /// Returns task status breakdown per group WITH date filter.
        /// Used for Chart 4 (Grouped Bar Chart).
        /// </summary>
        [HttpGet("studio/{studioId}/group-status")]
        public async Task<ActionResult<ApiResponse<StudioGroupStatusResponse>>> GetStudioGroupStatus(
            Guid studioId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var userId = ValidateAndGetUserId();

            DateOnly? start = startDate.HasValue ? DateOnly.FromDateTime(startDate.Value) : null;
            DateOnly? end = endDate.HasValue ? DateOnly.FromDateTime(endDate.Value) : null;

            var result = await _analyticsService.GetStudioGroupStatusAsync(studioId, start, end);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<StudioGroupStatusResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        // ==================== STUDIO GROUP ACTIVITY (Chart 5) ====================

        /// <summary>
        /// GET /api/analytics/studio/{studioId}/group-activity
        ///
        /// Returns activity heatmap data per group WITH date filter.
        /// Activity Level (0-4) is pre-calculated by backend with fixed thresholds.
        /// Used for Chart 5 (Activity Heatmap).
        ///
        /// Activity Score Formula:
        ///   Score = tasksCompleted×4 + tasksCreated×3 + tasksUpdated×2 + commentsCreated×1 + messagesSent×1
        ///
        /// Activity Level Thresholds (FIXED):
        ///   0 = 0, 1 = 1-5, 2 = 6-15, 3 = 16-30, 4 = 31+
        /// </summary>
        [HttpGet("studio/{studioId}/group-activity")]
        public async Task<ActionResult<ApiResponse<StudioGroupActivityResponse>>> GetStudioGroupActivity(
            Guid studioId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var userId = ValidateAndGetUserId();

            DateOnly? start = startDate.HasValue ? DateOnly.FromDateTime(startDate.Value) : null;
            DateOnly? end = endDate.HasValue ? DateOnly.FromDateTime(endDate.Value) : null;

            var result = await _analyticsService.GetStudioGroupActivityAsync(studioId, start, end);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<StudioGroupActivityResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }
    }
}
