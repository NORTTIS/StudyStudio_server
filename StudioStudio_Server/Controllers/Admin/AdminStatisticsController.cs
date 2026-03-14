using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;
using StudioStudio_Server.Utils;

namespace StudioStudio_Server.Controllers.Admin
{
    /// <summary>
    /// Admin Controller for Dashboard Statistics
    /// Route: /api/admin/statistics
    /// Only accessible by admin users
    /// </summary>
    [Route("api/admin/statistics")]
    [ApiController]
    [Authorize]
    public class AdminStatisticsController : ControllerBase
    {
        private readonly IAdminStatisticsService _statisticsService;
        private readonly IMessageService _messageService;

        public AdminStatisticsController(
            IAdminStatisticsService statisticsService,
            IMessageService messageService)
        {
            _statisticsService = statisticsService;
            _messageService = messageService;
        }

        /// <summary>
        /// [ADMIN] GET /api/admin/statistics/hourly-activity
        /// Get hourly activity heatmap data
        /// Query params:
        /// - StartDate: Start date (optional, default: all time)
        /// - EndDate: End date (optional, default: all time)
        /// Returns: Heatmap data with user activity by hour and day of week
        /// </summary>
        [HttpGet("hourly-activity")]
        public async Task<ActionResult<ApiResponse<HourlyActivityResponse>>> GetHourlyActivity([FromQuery] HourlyActivityRequest request)
        {
            JwtHelper.ValidateAdminUser(User);

            if (request.StartDate.HasValue && request.EndDate.HasValue && request.StartDate > request.EndDate)
            {
                throw new AppException(
                    ErrorCodes.RevenueInvalidDateRange,
                    StatusCodes.Status400BadRequest);
            }

            var response = await _statisticsService.GetHourlyActivityAsync(request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<HourlyActivityResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [ADMIN] GET /api/admin/statistics/report-status
        /// Get report status breakdown by period
        /// Query params:
        /// - StartDate: Start date (optional, default: all time)
        /// - EndDate: End date (optional, default: all time)
        /// - Period: "daily" | "weekly" | "monthly" (default: "monthly")
        /// Returns: Report counts by status for each period
        /// </summary>
        [HttpGet("report-status")]
        public async Task<ActionResult<ApiResponse<ReportStatusResponse>>> GetReportStatus([FromQuery] ReportStatusRequest request)
        {
            JwtHelper.ValidateAdminUser(User);

            if (request.StartDate.HasValue && request.EndDate.HasValue && request.StartDate > request.EndDate)
            {
                throw new AppException(
                    ErrorCodes.RevenueInvalidDateRange,
                    StatusCodes.Status400BadRequest);
            }

            var response = await _statisticsService.GetReportStatusAsync(request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<ReportStatusResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [ADMIN] GET /api/admin/statistics/user-distribution
        /// Get user distribution (Active/Inactive)
        /// Query params:
        /// - StartDate: Optional start date (default: last month)
        /// - EndDate: Optional end date (default: now)
        /// Returns: User count and percentage for each status
        /// </summary>
        [HttpGet("user-distribution")]
        public async Task<ActionResult<ApiResponse<UserDistributionResponse>>> GetUserDistribution([FromQuery] UserDistributionRequest request)
        {
            JwtHelper.ValidateAdminUser(User);

            if (request.StartDate.HasValue && request.EndDate.HasValue && request.StartDate > request.EndDate)
            {
                throw new AppException(
                    ErrorCodes.RevenueInvalidDateRange,
                    StatusCodes.Status400BadRequest);
            }

            var response = await _statisticsService.GetUserDistributionAsync(request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<UserDistributionResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [ADMIN] GET /api/admin/statistics/subscription-distribution
        /// Get subscription distribution (Free/Premium)
        /// Query params:
        /// - StartDate: Optional start date (default: last month)
        /// - EndDate: Optional end date (default: now)
        /// Returns: Subscription count, percentage, and revenue for each plan type
        /// </summary>
        [HttpGet("subscription-distribution")]
        public async Task<ActionResult<ApiResponse<SubscriptionDistributionResponse>>> GetSubscriptionDistribution([FromQuery] SubscriptionDistributionRequest request)
        {
            JwtHelper.ValidateAdminUser(User);

            if (request.StartDate.HasValue && request.EndDate.HasValue && request.StartDate > request.EndDate)
            {
                throw new AppException(
                    ErrorCodes.RevenueInvalidDateRange,
                    StatusCodes.Status400BadRequest);
            }

            var response = await _statisticsService.GetSubscriptionDistributionAsync(request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<SubscriptionDistributionResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [ADMIN] GET /api/admin/statistics/recent-activity
        /// Get recent activity feed
        /// Query params:
        /// - StartDate: Optional start date (default: last 7 days)
        /// - EndDate: Optional end date (default: now)
        /// - ItemCount: Number of items to return (default: 5)
        /// Returns: List of recent activities (user signups, reports, premium upgrades, group creations)
        /// </summary>
        [HttpGet("recent-activity")]
        public async Task<ActionResult<ApiResponse<RecentActivityResponse>>> GetRecentActivity([FromQuery] RecentActivityRequest request)
        {
            JwtHelper.ValidateAdminUser(User);

            if (request.StartDate.HasValue && request.EndDate.HasValue && request.StartDate > request.EndDate)
            {
                throw new AppException(
                    ErrorCodes.RevenueInvalidDateRange,
                    StatusCodes.Status400BadRequest);
            }

            var response = await _statisticsService.GetRecentActivityAsync(request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<RecentActivityResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [ADMIN] GET /api/admin/statistics/top-active-groups
        /// Get top active groups ranked by completion rate
        /// Query params:
        /// - StartDate: Optional start date (default: last month)
        /// - EndDate: Optional end date (default: now)
        /// - TopCount: Number of groups to return (default: 5)
        /// Returns: Top groups with member count, task stats, and completion rate
        /// </summary>
        [HttpGet("top-active-groups")]
        public async Task<ActionResult<ApiResponse<TopActiveGroupsResponse>>> GetTopActiveGroups([FromQuery] TopActiveGroupsRequest request)
        {
            JwtHelper.ValidateAdminUser(User);

            if (request.StartDate.HasValue && request.EndDate.HasValue && request.StartDate > request.EndDate)
            {
                throw new AppException(
                    ErrorCodes.RevenueInvalidDateRange,
                    StatusCodes.Status400BadRequest);
            }

            var response = await _statisticsService.GetTopActiveGroupsAsync(request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<TopActiveGroupsResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }
    }
}
