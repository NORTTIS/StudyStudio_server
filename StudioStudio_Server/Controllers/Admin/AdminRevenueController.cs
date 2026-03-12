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
    /// Admin Controller for Revenue Statistics
    /// Route: /api/admin/revenue
    /// Only accessible by admin users
    /// </summary>
    [Route("api/admin/revenue")]
    [ApiController]
    [Authorize]
    public class AdminRevenueController : ControllerBase
    {
        private readonly IRevenueService _revenueService;
        private readonly IMessageService _messageService;

        public AdminRevenueController(
            IRevenueService revenueService,
            IMessageService messageService)
        {
            _revenueService = revenueService;
            _messageService = messageService;
        }

        /// <summary>
        /// [ADMIN] GET /api/admin/revenue/overview
        /// Get revenue overview with key metrics:
        /// - TotalRevenue: All-time revenue
        /// - MonthlyRevenue: Current month revenue
        /// - YearlyRevenue: Current year revenue
        /// - TotalTransactions: Total payment count
        /// - SuccessfulTransactions: Successful payments
        /// - FailedTransactions: Failed/Cancelled payments
        /// - SuccessRate: Payment success percentage
        /// - ActiveSubscriptions: Currently active subscriptions
        /// - ARPU: Average Revenue Per User
        /// - MRR: Monthly Recurring Revenue
        /// </summary>
        [HttpGet("overview")]
        public async Task<ActionResult<ApiResponse<RevenueOverviewResponse>>> GetRevenueOverview()
        {
            JwtHelper.ValidateAdminUser(User);

            var response = await _revenueService.GetRevenueOverviewAsync();
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<RevenueOverviewResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [ADMIN] GET /api/admin/revenue/by-period
        /// Get revenue breakdown by time period
        /// Query params:
        /// - StartDate: Start date (required)
        /// - EndDate: End date (required)
        /// - Period: "daily" | "weekly" | "monthly" | "yearly" (default: "daily")
        /// - PlanId: Optional plan filter
        /// </summary>
        [HttpGet("by-period")]
        public async Task<ActionResult<ApiResponse<RevenueByPeriodResponse>>> GetRevenueByPeriod(
            [FromQuery] RevenueByPeriodRequest request)
        {
            JwtHelper.ValidateAdminUser(User);

            if (request.StartDate == default || request.EndDate == default)
            {
                throw new AppException(ErrorCodes.RevenueInvalidDateRange, StatusCodes.Status400BadRequest);
            }

            if (request.StartDate > request.EndDate)
            {
                throw new AppException(ErrorCodes.RevenueInvalidDateRange, StatusCodes.Status400BadRequest);
            }

            var response = await _revenueService.GetRevenueByPeriodAsync(request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<RevenueByPeriodResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [ADMIN] GET /api/admin/revenue/by-plan
        /// Get revenue breakdown by subscription plan
        /// Query params:
        /// - StartDate: Optional start date (default: start of current month)
        /// - EndDate: Optional end date (default: now)
        /// </summary>
        [HttpGet("by-plan")]
        public async Task<ActionResult<ApiResponse<RevenueByPlanResponse>>> GetRevenueByPlan(
            [FromQuery] RevenueByPlanRequest request)
        {
            JwtHelper.ValidateAdminUser(User);

            var response = await _revenueService.GetRevenueByPlanAsync(request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<RevenueByPlanResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [ADMIN] GET /api/admin/revenue/trends
        /// Get revenue trends with optional comparison to previous period
        /// Query params:
        /// - Period: "last7days" | "last30days" | "last90days" | "last12months" | "custom" (default: "last30days")
        /// - StartDate: Required if Period = "custom"
        /// - EndDate: Required if Period = "custom"
        /// - Comparison: Include previous period data (default: false)
        /// </summary>
        [HttpGet("trends")]
        public async Task<ActionResult<ApiResponse<RevenueTrendsResponse>>> GetRevenueTrends(
            [FromQuery] RevenueTrendsRequest request)
        {
            JwtHelper.ValidateAdminUser(User);

            if (request.Period.ToLower() == "custom")
            {
                if (!request.StartDate.HasValue || !request.EndDate.HasValue)
                {
                    throw new AppException(ErrorCodes.RevenueInvalidCustomPeriod, StatusCodes.Status400BadRequest);
                }
            }

            var response = await _revenueService.GetRevenueTrendsAsync(request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<RevenueTrendsResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [ADMIN] GET /api/admin/revenue/top-plans
        /// Get top performing subscription plans
        /// Query params:
        /// - Limit: Number of top plans (default: 5, max: 10)
        /// - StartDate: Optional start date
        /// - EndDate: Optional end date
        /// - SortBy: "revenue" | "subscriptions" | "growth" (default: "revenue")
        /// </summary>
        [HttpGet("top-plans")]
        public async Task<ActionResult<ApiResponse<TopPlansResponse>>> GetTopPlans(
            [FromQuery] TopPlansRequest request)
        {
            JwtHelper.ValidateAdminUser(User);

            if (request.Limit < 1 || request.Limit > 10)
            {
                throw new AppException(ErrorCodes.RevenueInvalidLimit, StatusCodes.Status400BadRequest);
            }

            var response = await _revenueService.GetTopPlansAsync(request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<TopPlansResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [ADMIN] GET /api/admin/revenue/transactions
        /// Get paginated revenue transactions
        /// Query params:
        /// - PageNumber: Page number (default: 1)
        /// - PageSize: Page size (default: 20, max: 100)
        /// - StartDate: Optional start date filter
        /// - EndDate: Optional end date filter
        /// - PlanId: Optional plan filter
        /// - PaymentStatus: Optional payment status filter
        /// - SearchTerm: Search by user email, name, or order code
        /// </summary>
        [HttpGet("transactions")]
        public async Task<ActionResult<ApiResponse<RevenueTransactionsResponse>>> GetTransactions(
            [FromQuery] RevenueTransactionsRequest request)
        {
            JwtHelper.ValidateAdminUser(User);

            var response = await _revenueService.GetRevenueTransactionsAsync(request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<RevenueTransactionsResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [ADMIN] GET /api/admin/revenue/mrr
        /// Get MRR breakdown by month for a specific year
        /// Query params:
        /// - Year: Year to get MRR data for (default: current year)
        /// </summary>
        [HttpGet("mrr")]
        public async Task<ActionResult<ApiResponse<MRRBreakdownResponse>>> GetMRRBreakdown(
            [FromQuery] MRRRequest request)
        {
            JwtHelper.ValidateAdminUser(User);

            var response = await _revenueService.GetMRRBreakdownAsync(request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<MRRBreakdownResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [ADMIN] GET /api/admin/revenue/export
        /// Export revenue report to Excel file
        /// Query params:
        /// - ReportType: "overview" | "by-period" | "by-plan" | "transactions" (default: "overview")
        /// - StartDate: Optional start date
        /// - EndDate: Optional end date
        /// - Period: "daily" | "weekly" | "monthly" | "yearly" (for by-period report)
        /// - IncludeCharts: Include chart data (default: false)
        /// </summary>
        [HttpGet("export")]
        public async Task<IActionResult> ExportRevenueReport([FromQuery] RevenueExportRequest request)
        {
            JwtHelper.ValidateAdminUser(User);

            var response = await _revenueService.ExportRevenueReportAsync(request);

            return File(
                response.FileContent,
                response.ContentType,
                response.FileName);
        }
    }
}
