using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SubscriptionPlanController(ISubscriptionPlanService subscriptionPlanService,
        IMessageService messageService) : ControllerBase
    {
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<SubscriptionPlanResponse>>> GetAllSubscription()
        {
            var result = await subscriptionPlanService.GetAllAsync();
            var message = messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<SubscriptionPlanResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }
    }
}
