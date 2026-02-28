using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service x? l? business logic cho Task Comments
    /// Note: Realtime commenting ðý?c handle b?i TaskCommentHub (SignalR)
    /// Service này ch? handle HTTP queries (l?ch s? comments)
    /// </summary>
    public class TaskCommentService : ITaskCommentService
    {
        private readonly ITaskCommentRepository _commentRepository;
        private readonly ITaskRepository _taskRepository;
        private readonly IGroupParticipantRepository _groupParticipantRepository;
        private readonly ILogger<TaskCommentService> _logger;

        public TaskCommentService(
            ITaskCommentRepository commentRepository,
            ITaskRepository taskRepository,
            IGroupParticipantRepository groupParticipantRepository,
            ILogger<TaskCommentService> logger)
        {
            _commentRepository = commentRepository;
            _taskRepository = taskRepository;
            _groupParticipantRepository = groupParticipantRepository;
            _logger = logger;
        }

        /// <summary>
        /// L?y l?ch s? comments c?a task (pagination)
        /// Validate:
        /// - Task ph?i t?n t?i
        /// - N?u GroupTask ? User ph?i là member c?a group
        /// - N?u PersonalTask ? User ph?i là owner
        /// Query:
        /// - Ði?u ki?n: TaskId = {taskId} AND IsDeleted = false AND ParentCommentId = null
        /// - Include: User info, Replies (1 level only)
        /// - S?p x?p: CreatedAt DESC (comment m?i nh?t trý?c)
        /// - Pagination: Skip({offset}).Take({limit})
        /// </summary>
        public async Task<TaskCommentListResponse> GetTaskCommentsAsync(
            Guid userId,
            Guid taskId,
            int limit,
            int offset)
        {
            var task = await ValidateTaskExistsAsync(taskId);
            await ValidateUserHasAccessToTaskAsync(task, userId);

            var comments = await _commentRepository.GetByTaskIdAsync(taskId, limit, offset);
            var totalCount = await _commentRepository.GetCountByTaskIdAsync(taskId);

            var commentDtos = comments
                .Select(MapToTaskCommentDto)
                .ToList();

            _logger.LogInformation(
                "Retrieved {Count} comments for task {TaskId} (Total: {Total}). UserId: {UserId}",
                commentDtos.Count, taskId, totalCount, userId);

            return new TaskCommentListResponse
            {
                TaskId = taskId,
                TotalComments = totalCount,
                Comments = commentDtos
            };
        }

        /// <summary>
        /// Validate task t?n t?i
        /// </summary>
        private async Task<TaskItem> ValidateTaskExistsAsync(Guid taskId)
        {
            var task = await _taskRepository.GetByIdAsync(taskId);

            if (task == null)
            {
                throw new AppException(
                    ErrorCodes.TaskNotFound,
                    StatusCodes.Status404NotFound);
            }

            return task;
        }

        /// <summary>
        /// Validate user có quy?n access task không
        /// GroupTask: User ph?i là member c?a group
        /// PersonalTask: User ph?i là owner
        /// </summary>
        private async Task ValidateUserHasAccessToTaskAsync(TaskItem task, Guid userId)
        {
            if (task.GroupId.HasValue)
            {
                var isUserInGroup = await _groupParticipantRepository
                    .IsUserInGroupAsync(task.GroupId.Value, userId);

                if (!isUserInGroup)
                {
                    throw new AppException(
                        ErrorCodes.GroupPermissionDenied,
                        StatusCodes.Status403Forbidden);
                }
            }
            else
            {
                if (task.OwnerId != userId)
                {
                    throw new AppException(
                        ErrorCodes.TaskPermissionDenied,
                        StatusCodes.Status403Forbidden);
                }
            }
        }

        /// <summary>
        /// Map TaskComment entity ? TaskCommentDto
        /// </summary>
        private TaskCommentDto MapToTaskCommentDto(TaskComment comment)
        {
            return new TaskCommentDto
            {
                CommentId = comment.CommentId,
                TaskId = comment.TaskId,
                UserId = comment.UserId,
                Content = comment.Content,
                CreatedAt = comment.CreatedAt,
                UpdatedAt = comment.UpdatedAt,
                IsDeleted = comment.IsDeleted,
                User = new UserDto
                {
                    Id = comment.User.UserId,
                    FirstName = comment.User.FirstName,
                    LastName = comment.User.LastName,
                    AvatarUrl = comment.User.AvatarUrl
                },
                ReplyCount = comment.Replies?.Count(r => !r.IsDeleted) ?? 0,
                Replies = comment.Replies?
                    .Where(r => !r.IsDeleted)
                    .Select(r => new TaskCommentDto
                    {
                        CommentId = r.CommentId,
                        TaskId = r.TaskId,
                        UserId = r.UserId,
                        Content = r.Content,
                        ParentCommentId = r.ParentCommentId,
                        CreatedAt = r.CreatedAt,
                        UpdatedAt = r.UpdatedAt,
                        IsDeleted = r.IsDeleted,
                        User = new UserDto
                        {
                            Id = r.User.UserId,
                            FirstName = r.User.FirstName,
                            LastName = r.User.LastName,
                            AvatarUrl = r.User.AvatarUrl
                        },
                        ReplyCount = 0,
                        Replies = null
                    })
                    .ToList()
            };
        }
    }
}
