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
    public class GroupTaskStatusController : ControllerBase
    {
        private readonly IGroupTaskStatusService _groupTaskStatusService;
        private readonly IMessageService _messageService;
        public GroupTaskStatusController(
            IGroupTaskStatusService groupTaskStatusService,
            IMessageService messageService)
        {
            _groupTaskStatusService = groupTaskStatusService;
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

        [HttpGet("{taskStatusId}/detail")]
        public async Task<ActionResult<ApiResponse<GroupTaskStatusResponse>>> GetTaskStatusDetail(Guid taskStatusId)
        {
            ValidateAndGetUserId();
            var result = await _groupTaskStatusService.GetGroupTaskStatusDetail(taskStatusId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<GroupTaskStatusResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        [HttpPost("{groupId}")]
        public async Task<ActionResult<ApiResponse<GroupTaskStatusResponse>>> CreateGroupTaskStatus(
           Guid groupId, [FromBody] GroupTaskStatusRequest request)
        {
            var userId = ValidateAndGetUserId();
            var result = await _groupTaskStatusService.CreateNewGroupTaskStatus(userId, groupId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessCreateTaskStatus);

            return Ok(ApiResponse<GroupTaskStatusResponse>.Success(
                ErrorCodes.SuccessCreateTaskStatus,
                message,
                result));
        }

        [HttpPut("{groupId}")]
        public async Task<ActionResult<ApiResponse<object>>> UpdateAllTaskStatusPosition(
            [FromBody] List<GroupTaskStatusPositionRequest> request, Guid groupId)
        {
            var userId = ValidateAndGetUserId();
            await _groupTaskStatusService.UpdateAllTaskStatusPosition(userId, groupId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessUpdateTaskStatus);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessUpdateTaskStatus,
                message,
                null));
        }

        [HttpPut("{groupId}/{statusId}")]
        public async Task<ActionResult<ApiResponse<object>>> UpdateTaskStatusDetail(
            [FromBody] GroupTaskStatusRequest request, Guid groupId, Guid statusId)
        {
            var userId = ValidateAndGetUserId();
            await _groupTaskStatusService.UpdateGroupTaskStatus(userId, groupId, statusId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessUpdateTaskStatus);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessUpdateTaskStatus,
                message,
                null));
        }

        [HttpDelete("{statusId}/group/{groupId}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteGroup(Guid statusId, Guid groupId)
        {
            var userId = ValidateAndGetUserId();
            await _groupTaskStatusService.SoftDeleteGroupTaskStatus(userId, groupId, statusId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessDeleteTaskStatus);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessDeleteTaskStatus,
                message,
                null));
        }
    }
}
