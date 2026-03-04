using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TaskController : ControllerBase
    {
        private readonly ITaskService _taskService;
        private readonly IMessageService _messageService;
        public TaskController(
            ITaskService taskService,
            IMessageService messageService)
        {
            _taskService = taskService;
            _messageService = messageService;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<TaskItemResponse>>> CreateGroupTask(
            [FromBody] TaskItemGroupRequest request)
        {
            var userId = ValidateAndGetUserId();
            var result = await _taskService.AddGroupTaskAsync(userId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessCreateTask);

            return Ok(ApiResponse<TaskItemResponse>.Success(
                ErrorCodes.SuccessCreateTask,
                message,
                result));
        }

        [HttpDelete("{groupId}/{taskId}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteTask(Guid groupId, Guid taskId)
        {
            var userId = ValidateAndGetUserId();
            await _taskService.SoftDeleteTaskAsync(userId, groupId, taskId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessDeleteTask);
            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessDeleteTask,
                message,
                null));
        }

        [HttpPut("{groupId}/{taskId}/restore")]
        public async Task<ActionResult<ApiResponse<object>>> RestoreTask(Guid groupId, Guid taskId)
        {
            var userId = ValidateAndGetUserId();
            await _taskService.RestoreGroupTaskAsync(userId, groupId, taskId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessRestoreTask);
            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessRestoreTask,
                message,
                null));
        }

        [HttpGet("{groupId}/deleted-task")]
        public async Task<ActionResult<ApiResponse<List<TaskDeleteResponse>>>> GetDeleteTaskList(Guid groupId)
        {
            var userId = ValidateAndGetUserId();
            var result = await _taskService.GetDeleteTaskListAsync(userId, groupId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);
            return Ok(ApiResponse<List<TaskDeleteResponse>>.Success(ErrorCodes.SuccessRestoreTask, message, result));
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
    }
}
