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
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class HomeController : ControllerBase
    {
        private readonly IHomeService _homeService;
        private readonly IMessageService _messageService;

        public HomeController(
            IHomeService homeService,
            IMessageService messageService)
        {
            _homeService = homeService;
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

        [HttpGet("/personal-status")]
        public async Task<ActionResult<ApiResponse<HomeTaskResponse>>> GetHome()
        {
            var userId = ValidateAndGetUserId();
            var result = await _homeService.GetGroupAssignedTaskAsync(userId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<HomeTaskResponse>.Success(
                ErrorCodes.SuccessGetGroup,
                message,
                result));
        }

        [HttpPost("/personal-status")]
        public async Task<ActionResult<ApiResponse<PersonalTaskStatusResponse>>> CreatePersonalTaskStatus(
            [FromBody] PersonalTaskStatusRequest request)
        {
            var userId = ValidateAndGetUserId();
            var result = await _homeService.CreateNewGroupTaskStatus(userId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessCreateTaskStatus);

            return Ok(ApiResponse<PersonalTaskStatusResponse>.Success(
                ErrorCodes.SuccessCreateTaskStatus,
                message,
                result));
        }

        [HttpPut("/personal-status/reorder")]
        public async Task<ActionResult<ApiResponse<object>>> ReorderTaskStatus(
            [FromBody] ReorderPersonalTaskStatusRequest request)
        {
            var userId = ValidateAndGetUserId();
            await _homeService.ReorderPersonalTaskStatus(userId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessUpdateTaskStatus);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessUpdateTaskStatus,
                message,
                null));
        }

        [HttpPut("/personal-status/{statusId}/update-detail")]
        public async Task<ActionResult<ApiResponse<object>>> UpdateTaskStatusDetail(
            [FromBody] PersonalTaskStatusRequest request, Guid statusId)
        {
            var userId = ValidateAndGetUserId();
            await _homeService.UpdatePersonalTaskStatus(userId, statusId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessUpdateTaskStatus);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessUpdateTaskStatus,
                message,
                null));
        }

        [HttpDelete("/personal-status/{statusId}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteGroup(Guid statusId)
        {
            var userId = ValidateAndGetUserId();
            await _homeService.DeletePersonalTaskStatus(userId, statusId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessDeleteTaskStatus);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessDeleteTaskStatus,
                message,
                null));
        }
    }
}
