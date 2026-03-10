using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PayOS.Models.Webhooks;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;
using StudioStudio_Server.Utils;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    /// <summary>
    /// Controller for PayOS payment processing
    /// Route: /api/payment
    /// </summary>
    [Route("api/payment")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IMessageService _messageService;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(IPaymentService paymentService, IMessageService messageService, ILogger<PaymentController> logger)
        {
            _paymentService = paymentService;
            _messageService = messageService;
            _logger = logger;
        }

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

        /// <summary>
        /// [AUTHORIZED] POST /api/payment/create
        /// Create a PayOS payment link for a premium subscription
        /// </summary>
        [HttpPost("create")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<CreatePaymentResponse>>> CreatePayment([FromBody] CreatePaymentRequest request)
        {
            var userId = ValidateAndGetUserId();
            var result = await _paymentService.CreatePaymentLinkAsync(userId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessPaymentCreated);

            return Ok(ApiResponse<CreatePaymentResponse>.Success(
                ErrorCodes.SuccessPaymentCreated,
                message,
                result));
        }

        /// <summary>
        /// [PUBLIC] POST /api/payment/webhook
        /// Receives PayOS payment webhook notifications
        /// </summary>
        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook([FromBody] Webhook webhookBody)
        {
            try
            {
                await _paymentService.HandleWebhookAsync(webhookBody);
            }
            catch (AppException ex) when (ex.Code == ErrorCodes.PaymentWebhookInvalid)
            {
                // Chữ ký sai — không retry, vẫn trả 200
                _logger.LogWarning("Invalid webhook signature");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook processing error");
                return StatusCode(500, new { success = false });
            }
            return Ok(new { success = true });
        }

        /// <summary>
        /// [AUTHORIZED] GET /api/payment/{paymentId}/status
        /// Get payment status for a specific payment
        /// </summary>
        [HttpGet("{paymentId:guid}/status")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<PaymentStatusResponse>>> GetStatus([FromRoute] Guid paymentId)
        {
            var userId = ValidateAndGetUserId();
            var result = await _paymentService.GetPaymentStatusAsync(userId, paymentId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<PaymentStatusResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        /// <summary>
        /// [AUTHORIZED] POST /api/payment/{orderCode}/cancel
        /// Cancel a pending payment
        /// </summary>
        [HttpPost("{orderCode:long}/cancel")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<PaymentStatusResponse>>> CancelPayment([FromRoute] long orderCode)
        {
            var userId = ValidateAndGetUserId();
            var result = await _paymentService.CancelPaymentAsync(userId, orderCode);
            var message = _messageService.GetMessage(ErrorCodes.SuccessPaymentCancelled);

            return Ok(ApiResponse<PaymentStatusResponse>.Success(
                ErrorCodes.SuccessPaymentCancelled,
                message,
                result));
        }

        [HttpGet("history")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<PaymentHistoryResponse>>> PaymentHistory()
        {
            var userId = ValidateAndGetUserId();
            var result = await _paymentService.GetPaymentHistoryAsync(userId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);
            return Ok(ApiResponse<PaymentHistoryResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }
    }
}
