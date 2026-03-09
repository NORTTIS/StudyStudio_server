using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PayOS.Models;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SubscriptionPlanController : ControllerBase
    {
        private ISubscriptionPlanService _subscriptionPlanService;
        private IMessageService _messageService;
        public SubscriptionPlanController(ISubscriptionPlanService subscriptionPlanService,
            IMessageService messageService)
        {
            _subscriptionPlanService = subscriptionPlanService;
            _messageService = messageService;
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

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<Models.DTOs.Response.ApiResponse<SubscriptionPlanResponse>>> GetAllSubscription()
        {
            var result = await _subscriptionPlanService.GetAllAsync();
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(Models.DTOs.Response.ApiResponse<SubscriptionPlanResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }
    }
}
