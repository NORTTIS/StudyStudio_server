using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    /// <summary>
    /// Controller qu?n l? Task Comments (l?ch s? comments)
    /// Route: /api/task-comments
    /// Note: Realtime commenting ðý?c handle b?i TaskCommentHub (SignalR)
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
        /// Xác th?c và l?y userId t? JWT token
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
        /// L?y l?ch s? comments c?a task (pagination)
        /// Validate:
        /// - Task ph?i t?n t?i
        /// - GroupTask: User ph?i là member c?a group
        /// - PersonalTask: User ph?i là owner
        /// Query:
        /// - Ch? l?y parent comments (ParentCommentId = null)
        /// - Include: User info, Replies (1 level only)
        /// - S?p x?p: CreatedAt DESC
        /// - Pagination: offset + limit
        /// Return: Danh sách comments + total count
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
