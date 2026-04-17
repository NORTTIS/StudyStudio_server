using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    /// <summary>
    /// Controller for managing Task Comments
    /// Route: /api/task-comments
    /// Features: Send comment, Reply to comment, Delete comment, Get comment history
    /// </summary>
    [Route("api/task-comments")]
    [ApiController]
    [Authorize]
    public class TaskCommentController(
        ITaskCommentService taskCommentService,
        IMessageService messageService,
        ILogger<TaskCommentController> logger) : ControllerBase
    {
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
        /// Query:
        /// - Only get parent comments (ParentCommentId = null)
        /// - Include: User info, Replies (1 level only)
        /// - Order by: CreatedAt DESC
        /// - Pagination: offset + limit
        /// </summary>
        [HttpGet("{taskId}")]
        public async Task<ActionResult<ApiResponse<TaskCommentListResponse>>> GetTaskComments(
            Guid taskId,
            [FromQuery] int limit = 100,
            [FromQuery] int offset = 0)
        {
            try
            {
                var userId = ValidateAndGetUserId();
                var result = await taskCommentService.GetTaskCommentsAsync(userId, taskId, limit, offset);
                var message = messageService.GetMessage(ErrorCodes.SuccessGetData);

                return Ok(ApiResponse<TaskCommentListResponse>.Success(
                    ErrorCodes.SuccessGetData,
                    message,
                    result));
            }
            catch (AppException ex)
            {
                logger.LogWarning("Error getting task comments: {Message}", ex.Message);
                return StatusCode(ex.HttpStatus, ApiResponse<object>.Error(ex.Code, ex.Message));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error getting task comments");
                var message = messageService.GetMessage(ErrorCodes.UnexpectedError);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error(ErrorCodes.UnexpectedError, message));
            }
        }

        /// <summary>
        /// [AUTHORIZED] POST /api/task-comments
        /// Send comment to task
        /// Validate:
        /// - Task must exist
        /// - GroupTask: User role must exclude Viewer (can be Commenter, Member, Moderator, Owner)
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<TaskCommentDto>>> SendComment(
            [FromBody] SendTaskCommentRequest request)
        {
            try
            {
                var userId = ValidateAndGetUserId();
                var result = await taskCommentService.SendCommentAsync(userId, request);
                var message = messageService.GetMessage(ErrorCodes.SuccessGetData);

                return Ok(ApiResponse<TaskCommentDto>.Success(
                    ErrorCodes.SuccessGetData,
                    message,
                    result));
            }
            catch (AppException ex)
            {
                logger.LogWarning("Error sending comment: {Message}", ex.Message);
                return StatusCode(ex.HttpStatus, ApiResponse<object>.Error(ex.Code, ex.Message));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error sending comment");
                var message = messageService.GetMessage(ErrorCodes.UnexpectedError);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error(ErrorCodes.UnexpectedError, message));
            }
        }

        /// <summary>
        /// [AUTHORIZED] POST /api/task-comments/reply
        /// Reply to task comment
        /// Validate:
        /// - Task must exist
        /// - Parent comment must exist and not deleted
        /// - Parent comment must belong to same task
        /// - GroupTask: User role must exclude Viewer
        /// </summary>
        [HttpPost("reply")]
        public async Task<ActionResult<ApiResponse<TaskCommentDto>>> ReplyToComment(
            [FromBody] ReplyToTaskCommentRequest request)
        {
            try
            {
                var userId = ValidateAndGetUserId();
                var result = await taskCommentService.ReplyToCommentAsync(userId, request);
                var message = messageService.GetMessage(ErrorCodes.SuccessGetData);

                return Ok(ApiResponse<TaskCommentDto>.Success(
                    ErrorCodes.SuccessGetData,
                    message,
                    result));
            }
            catch (AppException ex)
            {
                logger.LogWarning("Error replying to comment: {Message}", ex.Message);
                return StatusCode(ex.HttpStatus, ApiResponse<object>.Error(ex.Code, ex.Message));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error replying to comment");
                var message = messageService.GetMessage(ErrorCodes.UnexpectedError);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error(ErrorCodes.UnexpectedError, message));
            }
        }

        /// <summary>
        /// [AUTHORIZED] DELETE /api/task-comments/{commentId}
        /// Delete comment (soft delete) and all replies
        /// Validate:
        /// - Comment must exist
        /// - User has delete permission:
        ///   - Comment owner: Can delete own comment
        ///   - GroupTask: Group Owner/Moderator can delete any comment
        /// </summary>
        [HttpDelete("{commentId}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteComment(Guid commentId)
        {
            try
            {
                var userId = ValidateAndGetUserId();
                var request = new DeleteTaskCommentRequest { CommentId = commentId };
                await taskCommentService.DeleteCommentAsync(userId, request);
                var message = messageService.GetMessage(ErrorCodes.SuccessDeleteComment);

                return Ok(ApiResponse<object>.Success(
                    ErrorCodes.SuccessDeleteComment,
                    message));
            }
            catch (AppException ex)
            {
                logger.LogWarning("Error deleting comment: {Message}", ex.Message);
                return StatusCode(ex.HttpStatus, ApiResponse<object>.Error(ex.Code, ex.Message));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error deleting comment");
                var message = messageService.GetMessage(ErrorCodes.UnexpectedError);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.Error(ErrorCodes.UnexpectedError, message));
            }
        }
    }
}
