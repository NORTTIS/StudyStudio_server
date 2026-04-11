using Microsoft.AspNetCore.Http;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using StudioStudio_Server.Utils;
using System.Text.RegularExpressions;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service x? l? business logic cho Task Comments
    /// Handle: Send comment, Reply, Delete, Get history
    /// </summary>
    public class TaskCommentService : ITaskCommentService
    {
        private readonly ITaskCommentRepository _commentRepository;
        private readonly ITaskRepository _taskRepository;
        private readonly IGroupParticipantRepository _groupParticipantRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IUserRepository _userRepository;
        private readonly IAnnouncementRepository _announcementRepository;
        private readonly IUserAnnouncementService _userAnnouncementService;
        private readonly IMessageService _messageService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<TaskCommentService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IActivityLogService _activityLogService;

        public TaskCommentService(
            ITaskCommentRepository commentRepository,
            ITaskRepository taskRepository,
            IGroupParticipantRepository groupParticipantRepository,
            IUserRepository userRepository,
            IAnnouncementRepository announcementRepository,
            IUserAnnouncementService userAnnouncementService,
            IGroupRepository groupRepository,
            IMessageService messageService,
            INotificationService notificationService,
            ILogger<TaskCommentService> logger,
            IHttpContextAccessor httpContextAccessor,
            IActivityLogService activityLogService)
        {
            _commentRepository = commentRepository;
            _taskRepository = taskRepository;
            _groupParticipantRepository = groupParticipantRepository;
            _userRepository = userRepository;
            _announcementRepository = announcementRepository;
            _userAnnouncementService = userAnnouncementService;
            _groupRepository = groupRepository;
            _messageService = messageService;
            _notificationService = notificationService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _activityLogService = activityLogService;
        }

        /// <summary>
        /// Get comment history for task (pagination)
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
        /// Send comment to task
        /// Validate:
        /// - Task must exist
        /// - User must have access to task
        /// - For GroupTask: User role must be Commenter, Member, Moderator, or Owner (exclude Viewer)
        /// </summary>
        public async Task<TaskCommentDto> SendCommentAsync(Guid userId, SendTaskCommentRequest request)
        {
            var task = await ValidateTaskExistsAsync(request.TaskId);
            await ValidateUserCanCommentAsync(task, userId);

            var comment = new TaskComment
            {
                CommentId = Guid.NewGuid(),
                TaskId = request.TaskId,
                UserId = userId,
                Content = request.Content,
                ParentCommentId = null,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            await _commentRepository.AddAsync(comment);

            // Log comment creation activity
            var taskForLog = await _taskRepository.GetByIdAsync(request.TaskId);
            if (taskForLog != null)
            {
                await _activityLogService.LogCommentCreateAsync(userId, comment.CommentId, request.TaskId, taskForLog.GroupId);
            }

            var user = await _userRepository.GetByIdAsync(userId);
            var commentDto = MapToTaskCommentDtoWithUser(comment, user!);

            _logger.LogInformation(
                "Comment sent to task {TaskId} by user {UserId}",
                request.TaskId, userId);

            await HandleMentionNotificationsAsync(request.TaskId, userId, request.Content);

            return commentDto;
        }

        /// <summary>
        /// Reply to comment
        /// Validate:
        /// - Task must exist
        /// - Parent comment must exist and not deleted
        /// - Parent comment must belong to same task
        /// - User must have permission to comment
        /// </summary>
        public async Task<TaskCommentDto> ReplyToCommentAsync(Guid userId, ReplyToTaskCommentRequest request)
        {
            var task = await ValidateTaskExistsAsync(request.TaskId);
            await ValidateUserCanCommentAsync(task, userId);

            var parentComment = await _commentRepository.GetByIdAsync(request.ParentCommentId);

            if (parentComment == null || parentComment.IsDeleted)
            {
                _logger.LogError(
                    "Parent comment not found or deleted: ParentCommentId={ParentCommentId}",
                    request.ParentCommentId);
                throw new AppException(
                    ErrorCodes.MessageParentNotFound,
                    StatusCodes.Status404NotFound);
            }

            if (parentComment.TaskId != request.TaskId)
            {
                _logger.LogError(
                    "Parent comment TaskId mismatch: ParentTaskId={ParentTaskId}, RequestTaskId={RequestTaskId}",
                    parentComment.TaskId, request.TaskId);
                throw new AppException(
                    ErrorCodes.MessageParentNotFound,
                    StatusCodes.Status404NotFound);
            }

            var reply = new TaskComment
            {
                CommentId = Guid.NewGuid(),
                TaskId = request.TaskId,
                UserId = userId,
                Content = request.Content,
                ParentCommentId = request.ParentCommentId,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            await _commentRepository.AddAsync(reply);

            var user = await _userRepository.GetByIdAsync(userId);
            var replyDto = MapToTaskCommentDtoWithUser(reply, user!);

            _logger.LogInformation(
                "Reply sent to comment {ParentCommentId} in task {TaskId} by user {UserId}",
                request.ParentCommentId, request.TaskId, userId);

            await HandleMentionNotificationsAsync(request.TaskId, userId, request.Content);

            return replyDto;
        }

        /// <summary>
        /// Delete comment (soft delete) and all replies
        /// Validate:
        /// - Comment must exist
        /// - User has delete permission:
        ///   - Comment owner: Always can delete own comment
        ///   - For GroupTask: Group Owner/Moderator can delete any comment
        /// </summary>
        public async Task DeleteCommentAsync(Guid userId, DeleteTaskCommentRequest request)
        {
            var comment = await _commentRepository.GetByIdWithRepliesAsync(request.CommentId);
            if (comment == null)
            {
                throw new AppException(
                    ErrorCodes.MessageNotFound,
                    StatusCodes.Status404NotFound);
            }

            var task = await ValidateTaskExistsAsync(comment.TaskId);

            // Check if group is archived (non-owner cannot delete comments)
            if (task.GroupId.HasValue)
            {
                var group = await _groupRepository.GetByIdAsync(task.GroupId.Value);
                if (group != null && group.IsArchived)
                {
                    var userRole = await _groupParticipantRepository
                        .GetGroupRoleByUserIdAsync(userId, task.GroupId.Value);
                    if (userRole != GroupRole.Owner)
                    {
                        throw new AppException(ErrorCodes.GroupIsArchived, StatusCodes.Status403Forbidden);
                    }
                }
            }

            var hasPermission = await ValidateDeletePermissionAsync(comment, task, userId);
            if (!hasPermission)
            {
                throw new AppException(
                    ErrorCodes.MessagePermissionDenied,
                    StatusCodes.Status403Forbidden);
            }

            var replyCount = await _commentRepository.GetReplyCountAsync(request.CommentId);
            await _commentRepository.SoftDeleteWithRepliesAsync(request.CommentId);

            _logger.LogInformation(
                "Comment {CommentId} and {ReplyCount} replies deleted by user {UserId}",
                request.CommentId, replyCount, userId);
        }

        /// <summary>
        /// Extract user IDs from @mentions in content
        /// Pattern: @{userId} (UUID format)
        /// </summary>
        private List<Guid> ExtractTaggedUserIds(string content)
        {
            var matches = Regex.Matches(content, @"@([a-fA-F0-9\-]{36})", RegexOptions.None, TimeSpan.FromMilliseconds(200));
            return matches.Select(m => Guid.Parse(m.Groups[1].Value)).ToList();
        }

        private string ExtractPlainText(string content)
        {
            var text = Regex.Replace(content, @"@[a-fA-F0-9\-]{36}", "", RegexOptions.None, TimeSpan.FromMilliseconds(200));
            return Regex.Replace(text, @"\s+", " ", RegexOptions.None, TimeSpan.FromMilliseconds(200)).Trim();
        }

        /// <summary>
        /// Handle @mention notifications
        /// Create Announcement and UserAnnouncement for tagged users
        /// </summary>
        private async Task HandleMentionNotificationsAsync(
            Guid taskId,
            Guid senderId,
            string content)
        {
            var taggedUserIds = ExtractTaggedUserIds(content);

            if (!taggedUserIds.Any())
            {
                return;
            }

            var now = DateTime.UtcNow;
            var sender = await _userRepository.GetByIdAsync(senderId);
            var task = await _taskRepository.GetByIdAsync(taskId);
            var senderName = $"{sender!.FirstName} {sender.LastName}";

            var announcement = new Announcement
            {
                AnnouncementId = Guid.NewGuid(),
                Title = _messageService.GetMessage(ErrorCodes.AnnouncementTagTitle),
                Content = $"{senderName} {_messageService.GetMessage(ErrorCodes.AnnouncementTagTask)} {task!.Title} - {ExtractPlainText(content)}",
                Type = AnnouncementType.Mention,
                IsActive = true,
                CreatedBy = senderId,
                CreatedAt = now,
                UpdatedAt = now,
                PublishedAt = now,
                TaskId = taskId,
                GroupId = task.GroupId,
                SourceType = "comment"
            };

            await _announcementRepository.AddAsync(announcement);

            foreach (var taggedUserId in taggedUserIds)
            {
                var userAnnouncement = new UserAnnouncementRequest
                {
                    AnnouncementId = announcement.AnnouncementId,
                    MentionedId = taggedUserId,
                    CreatedBy = senderId,
                    IsRead = false,
                    CreatedAt = now
                };

                await _userAnnouncementService.AddAnnouncementAsync(userAnnouncement);
            }

            _logger.LogInformation(
                "Mention notifications sent to {Count} users in task {TaskId}",
                taggedUserIds.Count, taskId);
        }

        /// <summary>
        /// Validate task exists
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
        /// Validate user has access to task and can comment
        /// GroupTask: User must be member and role must exclude Viewer
        /// </summary>
        private async Task ValidateUserCanCommentAsync(TaskItem task, Guid userId)
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

                var userRole = await _groupParticipantRepository
                    .GetGroupRoleByUserIdAsync(userId, task.GroupId.Value);

                if (userRole == GroupRole.Viewer)
                {
                    throw new AppException(
                        ErrorCodes.GroupPermissionDenied,
                        StatusCodes.Status403Forbidden);
                }

                //  Check if group is archived (non-owner cannot interact)
                var group = await _groupRepository.GetByIdAsync(task.GroupId.Value);
                if (group != null && group.IsArchived && userRole != GroupRole.Owner)
                {
                    throw new AppException(ErrorCodes.GroupIsArchived, StatusCodes.Status403Forbidden);
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
        /// Validate delete permission
        /// Rules:
        /// - Comment owner: Can delete own comment
        /// - GroupTask: Group Owner/Moderator can delete any comment
        /// - PersonalTask: Task owner can delete any comment
        /// </summary>
        private async Task<bool> ValidateDeletePermissionAsync(TaskComment comment, TaskItem task, Guid userId)
        {
            if (comment.UserId == userId)
            {
                return true;
            }

            if (task.GroupId.HasValue)
            {
                var participant = await _groupParticipantRepository
                    .GetByUserAndGroupAsync(userId, task.GroupId.Value);

                return participant != null &&
                       (participant.Role == GroupRole.Owner ||
                        participant.Role == GroupRole.Moderator);
            }
            else
            {
                return task.OwnerId == userId;
            }
        }

        /// <summary>
        /// Validate user has access to task
        /// GroupTask: User must be member of group
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

                var group = await _groupRepository.GetByIdAsync(task.GroupId.Value);
                if (group != null && group.IsArchived)
                {
                    var userRole = await _groupParticipantRepository
                        .GetGroupRoleByUserIdAsync(userId, task.GroupId.Value);
                    if (userRole != GroupRole.Owner)
                    {
                        throw new AppException(ErrorCodes.GroupIsArchived, StatusCodes.Status403Forbidden);
                    }
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
        /// Map TaskComment entity to TaskCommentDto
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
                    AvatarUrl = AvatarUrlHelper.BuildAbsoluteAvatarUrl(comment.User.AvatarUrl, _httpContextAccessor.HttpContext)
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
                            AvatarUrl = AvatarUrlHelper.BuildAbsoluteAvatarUrl(r.User.AvatarUrl, _httpContextAccessor.HttpContext)
                        },
                        ReplyCount = 0,
                        Replies = null
                    })
                    .ToList()
            };
        }

        /// <summary>
        /// Map TaskComment entity to TaskCommentDto with provided user
        /// Used when comment is just created
        /// </summary>
        private TaskCommentDto MapToTaskCommentDtoWithUser(TaskComment comment, User user)
        {
            return new TaskCommentDto
            {
                CommentId = comment.CommentId,
                TaskId = comment.TaskId,
                UserId = comment.UserId,
                Content = comment.Content,
                ParentCommentId = comment.ParentCommentId,
                CreatedAt = comment.CreatedAt,
                UpdatedAt = comment.UpdatedAt,
                IsDeleted = comment.IsDeleted,
                User = new UserDto
                {
                    Id = user.UserId,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    AvatarUrl = AvatarUrlHelper.BuildAbsoluteAvatarUrl(user.AvatarUrl, _httpContextAccessor.HttpContext)
                },
                ReplyCount = 0,
                Replies = null
            };
        }
    }
}
