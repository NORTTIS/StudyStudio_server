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
        public async Task<ActionResult<ApiResponse<TaskItemResponse>>> CreateGroup(
            [FromBody] TaskItemGroupRequest request)
        {
            ValidateAndGetUserId();
            var result = await _taskService.AddGroupTaskAsync(request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessCreateTask);

            return Ok(ApiResponse<TaskItemResponse>.Success(
                ErrorCodes.SuccessCreateTask,
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
