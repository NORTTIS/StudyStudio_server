using Microsoft.AspNetCore.Authorization;
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
    public class TaskController(
        ITaskService taskService,
        IMessageService messageService) : ControllerBase
    {
        [HttpPost]
        public async Task<ActionResult<ApiResponse<TaskItemResponse>>> CreateGroupTask(
            [FromBody] TaskItemGroupRequest request)
        {
            var userId = ValidateAndGetUserId();
            var result = await taskService.AddGroupTaskAsync(userId, request);
            var message = messageService.GetMessage(ErrorCodes.SuccessCreateTask);

            return Ok(ApiResponse<TaskItemResponse>.Success(
                ErrorCodes.SuccessCreateTask,
                message,
                result));
        }

        [HttpPost("create-personal-task")]
        public async Task<ActionResult<ApiResponse<TaskItemResponse>>> CreatePersonalTask(
            [FromBody] TaskItemPersonalRequest request)
        {
            var userId = ValidateAndGetUserId();
            var result = await taskService.AddPersonalTaskAsync(userId, request);
            var message = messageService.GetMessage(ErrorCodes.SuccessCreateTask);

            return Ok(ApiResponse<TaskItemResponse>.Success(
                ErrorCodes.SuccessCreateTask,
                message,
                result));
        }

        [HttpPut("{groupId}/{taskId}")]
        public async Task<ActionResult<ApiResponse<TaskItemResponse>>> UpdateGroupTask(
            Guid groupId, Guid taskId, [FromBody] UpdateTaskRequest request)
        {
            var userId = ValidateAndGetUserId();
            var result = await taskService.UpdateGroupTaskAsync(userId, groupId, taskId, request);
            var message = messageService.GetMessage(ErrorCodes.SuccessUpdateTask);

            return Ok(ApiResponse<TaskItemResponse>.Success(
                ErrorCodes.SuccessUpdateTask,
                message,
                result));
        }

        [HttpPut("update-personal-task/{taskId}")]
        public async Task<ActionResult<ApiResponse<TaskItemResponse>>> UpdatePersonalTask(
            Guid taskId, [FromBody] UpdatePersonalTaskRequest request)
        {
            var userId = ValidateAndGetUserId();
            var result = await taskService.UpdatePersonalTaskAsync(userId, taskId, request);
            var message = messageService.GetMessage(ErrorCodes.SuccessUpdateTask);

            return Ok(ApiResponse<TaskItemResponse>.Success(
                ErrorCodes.SuccessUpdateTask,
                message,
                result));
        }

        [HttpDelete("{groupId}/{taskId}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteTask(Guid groupId, Guid taskId)
        {
            var userId = ValidateAndGetUserId();
            await taskService.SoftDeleteTaskAsync(userId, groupId, taskId);
            var message = messageService.GetMessage(ErrorCodes.SuccessDeleteTask);
            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessDeleteTask,
                message,
                null));
        }

        [HttpDelete("delete-personal-task/{taskId}")]
        public async Task<ActionResult<ApiResponse<object>>> DeletePersonalTask(Guid taskId)
        {
            var userId = ValidateAndGetUserId();
            await taskService.DeletePersonalTaskAsync(userId, taskId);
            var message = messageService.GetMessage(ErrorCodes.SuccessDeleteTask);
            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessDeleteTask,
                message,
                null));
        }

        [HttpPut("{groupId}/{taskId}/restore")]
        public async Task<ActionResult<ApiResponse<object>>> RestoreTask(Guid groupId, Guid taskId)
        {
            var userId = ValidateAndGetUserId();
            await taskService.RestoreGroupTaskAsync(userId, groupId, taskId);
            var message = messageService.GetMessage(ErrorCodes.SuccessRestoreTask);
            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessRestoreTask,
                message,
                null));
        }

        [HttpGet("{groupId}/deleted-task")]
        public async Task<ActionResult<ApiResponse<List<TaskDeleteResponse>>>> GetDeleteTaskList(Guid groupId)
        {
            var userId = ValidateAndGetUserId();
            var result = await taskService.GetDeleteTaskListAsync(userId, groupId);
            var message = messageService.GetMessage(ErrorCodes.SuccessGetData);
            return Ok(ApiResponse<List<TaskDeleteResponse>>.Success(ErrorCodes.SuccessRestoreTask, message, result));
        }

        [HttpPut("{groupId}/reorder")]
        public async Task<ActionResult<ApiResponse<object>>> ReorderTask(
            Guid groupId, [FromBody] ReorderTaskRequest request)
        {
            var userId = ValidateAndGetUserId();
            await taskService.ReorderTaskAsync(userId, groupId, request);
            var message = messageService.GetMessage(ErrorCodes.SuccessUpdateTask);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessUpdateTask,
                message,
                null));
        }
        [HttpPut("reorder-personal-task")]
        public async Task<ActionResult<ApiResponse<object>>> ReorderPersonalTask(
            [FromBody] ReorderTaskRequest request)
        {
            var userId = ValidateAndGetUserId();
            await taskService.ReorderPersonalTaskAsync(userId, request);
            var message = messageService.GetMessage(ErrorCodes.SuccessUpdateTask);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessUpdateTask,
                message,
                null));
        }

        [HttpDelete("{groupId}/{taskId}/permanent")]
        public async Task<ActionResult<ApiResponse<object>>> PermanentDeleteGroupTask(Guid groupId, Guid taskId)
        {
            var userId = ValidateAndGetUserId();
            await taskService.PermanentDeleteGroupTaskAsync(userId, groupId, taskId);
            var message = messageService.GetMessage(ErrorCodes.SuccessDeleteTask);
            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessDeleteTask,
                message,
                null));
        }

        [HttpGet("{taskId}/group")]
        public async Task<ActionResult<ApiResponse<TaskGroupResponse>>> GetTaskGroup(Guid taskId)
        {
            var userId = ValidateAndGetUserId();
            var result = await taskService.GetTaskGroupAsync(taskId, userId);
            if (result == null)
            {
                throw new AppException(ErrorCodes.TaskNotFound, StatusCodes.Status404NotFound);
            }

            var message = messageService.GetMessage(ErrorCodes.SuccessGetData);
            return Ok(ApiResponse<TaskGroupResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
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
