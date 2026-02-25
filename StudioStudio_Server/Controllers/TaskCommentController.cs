using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Repositories.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    [Route("api/task-comments")]
    [ApiController]
    [Authorize]
    public class TaskCommentController : ControllerBase
    {
        private readonly ITaskCommentRepository _commentRepository;
        private readonly ITaskRepository _taskRepository;
        private readonly IGroupParticipantRepository _groupParticipantRepository;
        private readonly ILogger<TaskCommentController> _logger;

        public TaskCommentController(
            ITaskCommentRepository commentRepository,
            ITaskRepository taskRepository,
            IGroupParticipantRepository groupParticipantRepository,
            ILogger<TaskCommentController> logger)
        {
            _commentRepository = commentRepository;
            _taskRepository = taskRepository;
            _groupParticipantRepository = groupParticipantRepository;
            _logger = logger;
        }

        [HttpGet("{taskId}")]
        public async Task<ActionResult<ApiResponse<TaskCommentListResponse>>> GetTaskComments(
            Guid taskId,
            [FromQuery] int limit = 100,
            [FromQuery] int offset = 0)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null)
            {
                throw new AppException(ErrorCodes.TaskNotFound, StatusCodes.Status404NotFound);
            }

            if (task.GroupId.HasValue)
            {
                var isUserInGroup = await _groupParticipantRepository.IsUserInGroupAsync(task.GroupId.Value, userId);
                if (!isUserInGroup)
                {
                    throw new AppException(ErrorCodes.GroupPermissionDenied, StatusCodes.Status403Forbidden);
                }
            }
            else
            {
                if (task.OwnerId != userId)
                {
                    throw new AppException(ErrorCodes.TaskPermissionDenied, StatusCodes.Status403Forbidden);
                }
            }

            var comments = await _commentRepository.GetByTaskIdAsync(taskId, limit, offset);
            var totalCount = await _commentRepository.GetCountByTaskIdAsync(taskId);

            var commentDtos = comments.Select(c => new TaskCommentDto
            {
                CommentId = c.CommentId,
                TaskId = c.TaskId,
                UserId = c.UserId,
                Content = c.Content,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                IsDeleted = c.IsDeleted,
                User = new UserDto
                {
                    Id = c.User.UserId,
                    FirstName = c.User.FirstName,
                    LastName = c.User.LastName,
                    AvatarUrl = c.User.AvatarUrl
                }
            }).ToList();

            var response = new TaskCommentListResponse
            {
                TaskId = taskId,
                TotalComments = totalCount,
                Comments = commentDtos
            };

            return Ok(ApiResponse<TaskCommentListResponse>.Success(
                ErrorCodes.SuccessGetData,
                "Comments retrieved successfully",
                response));
        }
    }
}
