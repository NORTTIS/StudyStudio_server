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
    /// Admin Controller for managing Subscription Plans
    /// Route: /api/admin/subscription-plans
    /// Only accessible by admin users
    /// </summary>
    [Route("api/admin/subscription-plans")]
    [ApiController]
    [Authorize]
    public class AdminSubscriptionPlanController(
        ISubscriptionPlanService subscriptionPlanService,
        IMessageService messageService) : ControllerBase
    {
        /// <summary>
        /// [ADMIN] GET /api/admin/subscription-plans/statistics
        /// Get subscription plan statistics and information
        /// Returns:
        /// - UserStats: Total active users, Free users, Premium users, Conversion rate
        /// - Plans: List of all plans (including inactive) with subscriber counts
        /// Only admin can access this endpoint
        /// </summary>
        [HttpGet("statistics")]
        public async Task<ActionResult<ApiResponse<SubscriptionStatisticsResponse>>> GetStatistics()
        {
            JwtHelper.ValidateAdminUser(User);

            var response = await subscriptionPlanService.GetStatisticsAsync();
            var message = messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<SubscriptionStatisticsResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [ADMIN] PUT /api/admin/subscription-plans
        /// Update subscription plan information
        /// Validate:
        /// - Plan must exist
        /// - Only admin can update plans
        /// Updates: PlanName, Price, BillingCycle, Description, Limits, IsActive
        /// Returns: Updated plan details with current subscriber count
        /// </summary>
        [HttpPut]
        public async Task<ActionResult<ApiResponse<SubscriptionPlanDetail>>> UpdatePlan(
            [FromBody] UpdateSubscriptionPlanRequest request)
        {
            JwtHelper.ValidateAdminUser(User);

            var response = await subscriptionPlanService.UpdatePlanAsync(request);
            var message = messageService.GetMessage(ErrorCodes.SuccessUpdateSubscriptionPlan);

            return Ok(ApiResponse<SubscriptionPlanDetail>.Success(
                ErrorCodes.SuccessUpdateSubscriptionPlan,
                message,
                response));
        }
    }
}
