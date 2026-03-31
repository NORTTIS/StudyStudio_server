using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Response;
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

        public AnalyticsController(IAnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
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

            return Ok(ApiResponse<GroupSummaryResponse>.Success(
                ErrorCodes.SuccessGetData,
                "Data retrieved successfully",
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

            return Ok(ApiResponse<List<MemberProgressTrendData>>.Success(
                ErrorCodes.SuccessGetData,
                "Data retrieved successfully",
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

            return Ok(ApiResponse<List<MemberHeatmapData>>.Success(
                ErrorCodes.SuccessGetData,
                "Data retrieved successfully",
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

            return Ok(ApiResponse<List<MemberContributionData>>.Success(
                ErrorCodes.SuccessGetData,
                "Data retrieved successfully",
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

            return Ok(ApiResponse<List<GroupComparisonData>>.Success(
                ErrorCodes.SuccessGetData,
                "Data retrieved successfully",
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

            return Ok(ApiResponse<StudioGroupHeatmapResponse>.Success(
                ErrorCodes.SuccessGetData,
                "Data retrieved successfully",
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

            return Ok(ApiResponse<StudioOverviewResponse>.Success(
                ErrorCodes.SuccessGetData,
                "Data retrieved successfully",
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

            return Ok(ApiResponse<StudioCompletionTrendResponse>.Success(
                ErrorCodes.SuccessGetData,
                "Data retrieved successfully",
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

            return Ok(ApiResponse<StudioGroupStatusResponse>.Success(
                ErrorCodes.SuccessGetData,
                "Data retrieved successfully",
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

            return Ok(ApiResponse<StudioGroupActivityResponse>.Success(
                ErrorCodes.SuccessGetData,
                "Data retrieved successfully",
                result));
        }

        // ==================== PERSONAL ANALYTICS (AnalysisHome) ====================

        /// <summary>
        /// GET /api/analytics/user/{userId}/kpi-summary
        /// KPI summary: total tasks, completed, overdue, completion rate, avg time
        /// </summary>
        [HttpGet("user/{userId}/kpi-summary")]
        public async Task<ActionResult<ApiResponse<UserKpiSummaryResponse>>> GetUserKpiSummary(Guid userId)
        {
            var currentUserId = ValidateAndGetUserId();
            if (currentUserId != userId)
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);

            var result = await _analyticsService.GetUserKpiSummaryAsync(userId);
            return Ok(ApiResponse<UserKpiSummaryResponse>.Success(
                ErrorCodes.SuccessGetData, "Data retrieved successfully", result));
        }

        /// <summary>
        /// GET /api/analytics/user/{userId}/task-status
        /// Task status donut: Hoàn thành, Đang làm, Chưa bắt đầu, Quá hạn
        /// </summary>
        [HttpGet("user/{userId}/task-status")]
        public async Task<ActionResult<ApiResponse<UserTaskStatusResponse>>> GetUserTaskStatus(Guid userId)
        {
            var currentUserId = ValidateAndGetUserId();
            if (currentUserId != userId)
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);

            var result = await _analyticsService.GetUserTaskStatusAsync(userId);
            return Ok(ApiResponse<UserTaskStatusResponse>.Success(
                ErrorCodes.SuccessGetData, "Data retrieved successfully", result));
        }

        /// <summary>
        /// GET /api/analytics/user/{userId}/group-rankings
        /// Cross-studio group rankings sorted by score
        /// </summary>
        [HttpGet("user/{userId}/group-rankings")]
        public async Task<ActionResult<ApiResponse<UserGroupRankingsResponse>>> GetUserGroupRankings(Guid userId)
        {
            var currentUserId = ValidateAndGetUserId();
            if (currentUserId != userId)
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);

            var result = await _analyticsService.GetUserGroupRankingsAsync(userId);
            return Ok(ApiResponse<UserGroupRankingsResponse>.Success(
                ErrorCodes.SuccessGetData, "Data retrieved successfully", result));
        }

        /// <summary>
        /// GET /api/analytics/user/{userId}/productivity-trend?period=30
        /// 30-day productivity trend (area chart)
        /// </summary>
        [HttpGet("user/{userId}/productivity-trend")]
        public async Task<ActionResult<ApiResponse<UserProductivityTrendResponse>>> GetUserProductivityTrend(
            Guid userId,
            [FromQuery] int period = 30)
        {
            var currentUserId = ValidateAndGetUserId();
            if (currentUserId != userId)
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);

            // Validate period bounds
            if (period < 1 || period > 365)
                throw new AppException(
                    ErrorCodes.ValidationInvalidRange,
                    StatusCodes.Status400BadRequest);

            var result = await _analyticsService.GetUserProductivityTrendAsync(userId, period);
            return Ok(ApiResponse<UserProductivityTrendResponse>.Success(
                ErrorCodes.SuccessGetData, "Data retrieved successfully", result));
        }

        /// <summary>
        /// GET /api/analytics/user/{userId}/on-time-overview
        /// On-time vs overdue donut
        /// </summary>
        [HttpGet("user/{userId}/on-time-overview")]
        public async Task<ActionResult<ApiResponse<UserOnTimeOverviewResponse>>> GetUserOnTimeOverview(Guid userId)
        {
            var currentUserId = ValidateAndGetUserId();
            if (currentUserId != userId)
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);

            var result = await _analyticsService.GetUserOnTimeOverviewAsync(userId);
            return Ok(ApiResponse<UserOnTimeOverviewResponse>.Success(
                ErrorCodes.SuccessGetData, "Data retrieved successfully", result));
        }

        /// <summary>
        /// GET /api/analytics/user/{userId}/priority-distribution
        /// Task distribution by priority: Cao, Trung bình, Thấp
        /// </summary>
        [HttpGet("user/{userId}/priority-distribution")]
        public async Task<ActionResult<ApiResponse<UserPriorityDistributionResponse>>> GetUserPriorityDistribution(Guid userId)
        {
            var currentUserId = ValidateAndGetUserId();
            if (currentUserId != userId)
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);

            var result = await _analyticsService.GetUserPriorityDistributionAsync(userId);
            return Ok(ApiResponse<UserPriorityDistributionResponse>.Success(
                ErrorCodes.SuccessGetData, "Data retrieved successfully", result));
        }

        /// <summary>
        /// GET /api/analytics/user/{userId}/urgency-distribution
        /// Task distribution by urgency: Khẩn cấp, Cao, Trung bình, Thấp
        /// </summary>
        [HttpGet("user/{userId}/urgency-distribution")]
        public async Task<ActionResult<ApiResponse<UserUrgencyDistributionResponse>>> GetUserUrgencyDistribution(Guid userId)
        {
            var currentUserId = ValidateAndGetUserId();
            if (currentUserId != userId)
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);

            var result = await _analyticsService.GetUserUrgencyDistributionAsync(userId);
            return Ok(ApiResponse<UserUrgencyDistributionResponse>.Success(
                ErrorCodes.SuccessGetData, "Data retrieved successfully", result));
        }

        /// <summary>
        /// GET /api/analytics/user/{userId}/benchmark?weeks=7&groupId={guid}
        /// Weekly performance benchmark (user vs group avg)
        /// </summary>
        [HttpGet("user/{userId}/benchmark")]
        public async Task<ActionResult<ApiResponse<UserBenchmarkResponse>>> GetUserBenchmark(
            Guid userId,
            [FromQuery] int weeks = 7,
            [FromQuery] Guid? groupId = null)
        {
            var currentUserId = ValidateAndGetUserId();
            if (currentUserId != userId)
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);

            // Validate weeks bounds
            if (weeks < 1 || weeks > 52)
                throw new AppException(
                    ErrorCodes.ValidationInvalidRange,
                    StatusCodes.Status400BadRequest);

            var result = await _analyticsService.GetUserBenchmarkAsync(userId, weeks, groupId);
            return Ok(ApiResponse<UserBenchmarkResponse>.Success(
                ErrorCodes.SuccessGetData, "Data retrieved successfully", result));
        }

        /// <summary>
        /// GET /api/analytics/user/{userId}/risk-alerts?limit=10
        /// Risk alerts: overdue, due soon, stuck tasks
        /// </summary>
        [HttpGet("user/{userId}/risk-alerts")]
        public async Task<ActionResult<ApiResponse<UserRiskAlertsResponse>>> GetUserRiskAlerts(
            Guid userId,
            [FromQuery] int limit = 10)
        {
            var currentUserId = ValidateAndGetUserId();
            if (currentUserId != userId)
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);

            // Validate limit bounds
            if (limit < 1 || limit > 100)
                throw new AppException(
                    ErrorCodes.ValidationInvalidRange,
                    StatusCodes.Status400BadRequest);

            var result = await _analyticsService.GetUserRiskAlertsAsync(userId, limit);
            return Ok(ApiResponse<UserRiskAlertsResponse>.Success(
                ErrorCodes.SuccessGetData, "Data retrieved successfully", result));
        }
    }
}
