using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    /// <summary>
    /// Controller for managing Task Comments (comment history)
    /// Route: /api/task-comments
    /// Note: Realtime commenting is handled by TaskCommentHub (SignalR)
    /// </summary>
    [Route("api/task-comments")]
    [ApiController]
    [Authorize]
    public class TaskCommentController : ControllerBase
    {
        private readonly ITaskCommentService _taskCommentService;
        private readonly IMessageService _messageService;

        public TaskCommentController(
            ITaskCommentService taskCommentService,
            IMessageService messageService)
        {
            _taskCommentService = taskCommentService;
            _messageService = messageService;
        }

        /// <summary>
        /// Authenticate and get userId from JWT token
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

            return userId;
        }

        /// <summary>
        /// [AUTHORIZED] GET /api/task-comments/{taskId}?limit=100&offset=0
        /// Get comment history for task (pagination)
        /// Validate:
        /// - Task must exist
        /// - GroupTask: User must be member of group
        /// - PersonalTask: User must be owner
        /// Query:
        /// - Only get parent comments (ParentCommentId = null)
        /// - Include: User info, Replies (1 level only)
        /// - Order by: CreatedAt DESC
        /// - Pagination: offset + limit
        /// Return: List of comments + total count
        /// </summary>
        [HttpGet("{taskId}")]
        public async Task<ActionResult<ApiResponse<TaskCommentListResponse>>> GetTaskComments(
            Guid taskId,
            [FromQuery] int limit = 100,
            [FromQuery] int offset = 0)
        {
            var userId = ValidateAndGetUserId();
            var result = await _taskCommentService.GetTaskCommentsAsync(userId, taskId, limit, offset);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<TaskCommentListResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }
    }
}
