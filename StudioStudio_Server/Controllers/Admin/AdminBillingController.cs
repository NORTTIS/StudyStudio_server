using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services;
using StudioStudio_Server.Services.Interfaces;
using StudioStudio_Server.Utils;

namespace StudioStudio_Server.Controllers.Admin
{
    /// <summary>
    /// Admin Controller for managing Billing/Payments
    /// Route: /api/admin/billing
    /// Only accessible by admin users
    /// </summary>
    [Route("api/admin/billing")]
    [ApiController]
    [Authorize]
    public class AdminBillingController(
        IPaymentService paymentService,
        IMessageService messageService) : ControllerBase
    {
        /// <summary>
        /// [ADMIN] GET /api/admin/billing/history
        /// Get paginated billing history with filters
        /// Query params:
        /// - searchTerm: Search by userName, userEmail, or invoiceId (orderCode)
        /// - paymentStatus: Filter by payment status (PENDING, SUCCESS, CANCELLED, FAILED)
        /// - pageNumber: Page number (default: 1)
        /// - pageSize: Page size (default: 10, max: 100)
        /// Returns: Paginated list of billing history items with user and plan info
        /// Only admin can access this endpoint
        /// </summary>
        [HttpGet("history")]
        public async Task<ActionResult<ApiResponse<BillingHistoryResponse>>> GetBillingHistory(
            [FromQuery] GetBillingHistoryRequest request)
        {
            JwtHelper.ValidateAdminUser(User);

            // Set default values if not provided
            if (request.PageNumber < 1) request.PageNumber = 1;
            if (request.PageSize < 1) request.PageSize = 10;
            if (request.PageSize > 100) request.PageSize = 100;

            var response = await paymentService.GetBillingHistoryAsync(request);
            var message = messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<BillingHistoryResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }
    }
}
