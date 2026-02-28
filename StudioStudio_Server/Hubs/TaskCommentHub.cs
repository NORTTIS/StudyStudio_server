using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Hubs
{
    /// <summary>
    /// SignalR Hub x? l? realtime Task Comments
    /// Route: /hubs/task-comment
    /// Features: Join/Leave task room, Send comment, Reply to comment, Delete comment
    /// </summary>
    [Authorize]
    public class TaskCommentHub : Hub
    {
        private readonly ITaskCommentRepository _commentRepository;
        private readonly ITaskRepository _taskRepository;
        private readonly IGroupParticipantRepository _groupParticipantRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMessageService _messageService;
        private readonly ILogger<TaskCommentHub> _logger;

        public TaskCommentHub(
            ITaskCommentRepository commentRepository,
            ITaskRepository taskRepository,
            IGroupParticipantRepository groupParticipantRepository,
            IUserRepository userRepository,
            IMessageService messageService,
            ILogger<TaskCommentHub> logger)
        {
            _commentRepository = commentRepository;
            _taskRepository = taskRepository;
            _groupParticipantRepository = groupParticipantRepository;
            _userRepository = userRepository;
            _messageService = messageService;
            _logger = logger;
        }

        /// <summary>
        /// L?y userId t? SignalR Context
        /// </summary>
        private Guid GetUserId()
        {
            var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(
                    ErrorCodes.AuthInvalidCredential,
                    StatusCodes.Status401Unauthorized);
            }

            return userId;
        }

        /// <summary>
        /// L?y localized error message theo ngôn ng? c?a user
        /// </summary>
        private async Task<string> GetLocalizedMessageAsync(string errorCode)
        {
            try
            {
                var userId = GetUserId();
                var user = await _userRepository.GetByIdAsync(userId);

                if (user != null && !string.IsNullOrEmpty(user.Language))
                {
                    if (Context.GetHttpContext() != null)
                    {
                        Context.GetHttpContext().Request.Headers["Accept-Language"] = user.Language;
                    }
                }

                return _messageService.GetMessage(errorCode);
            }
            catch
            {
                return _messageService.GetMessage(errorCode);
            }
        }

        /// <summary>
        /// Validate user có quy?n access task không
        /// GroupTask: User ph?i là member c?a group
        /// PersonalTask: User ph?i là owner
        /// </summary>
        private async Task<bool> ValidateTaskAccessAsync(TaskItem task, Guid userId)
        {
            if (task.GroupId.HasValue)
            {
                return await _groupParticipantRepository.IsUserInGroupAsync(task.GroupId.Value, userId);
            }
            else
            {
                return task.OwnerId == userId;
            }
        }

        /// <summary>
        /// Validate user có quy?n delete comment không
        /// Owner c?a comment: Luôn có quy?n
        /// Group Owner/Moderator: Có quy?n delete b?t k? comment nào
        /// Personal task owner: Có quy?n delete b?t k? comment nào
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
        /// Join task room ð? nh?n realtime comments
        /// Validate: User ph?i có quy?n access task
        /// SignalR Group Name: "task_{taskId}"
        /// </summary>
        public async Task JoinTask(Guid taskId)
        {
            try
            {
                var userId = GetUserId();
                var task = await _taskRepository.GetByIdAsync(taskId);

                if (task == null)
                {
                    var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.TaskNotFound);
                    await Clients.Caller.SendAsync("Error", errorMsg);
                    return;
                }

                var hasAccess = await ValidateTaskAccessAsync(task, userId);
                if (!hasAccess)
                {
                    var errorMsg = await GetLocalizedMessageAsync(
                        task.GroupId.HasValue
                            ? ErrorCodes.GroupPermissionDenied
                            : ErrorCodes.TaskPermissionDenied);
                    await Clients.Caller.SendAsync("Error", errorMsg);
                    return;
                }

                await Groups.AddToGroupAsync(Context.ConnectionId, $"task_{taskId}");
                _logger.LogInformation("User {UserId} joined task {TaskId}", userId, taskId);

                await Clients.Group($"task_{taskId}").SendAsync("UserJoined", new
                {
                    UserId = userId,
                    TaskId = taskId,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining task");
                var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.UnexpectedError);
                await Clients.Caller.SendAsync("Error", errorMsg);
            }
        }

        /// <summary>
        /// Leave task room
        /// </summary>
        public async Task LeaveTask(Guid taskId)
        {
            try
            {
                var userId = GetUserId();

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"task_{taskId}");
                _logger.LogInformation("User {UserId} left task {TaskId}", userId, taskId);

                await Clients.Group($"task_{taskId}").SendAsync("UserLeft", new
                {
                    UserId = userId,
                    TaskId = taskId,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving task");
                var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.UnexpectedError);
                await Clients.Caller.SendAsync("Error", errorMsg);
            }
        }

        /// <summary>
        /// G?i comment m?i vào task (realtime)
        /// Validate: User ph?i có quy?n access task
        /// Broadcast: "ReceiveComment" event t?i t?t c? members trong task room
        /// </summary>
        public async Task SendComment(SendTaskCommentRequest request)
        {
            try
            {
                var userId = GetUserId();
                var task = await _taskRepository.GetByIdAsync(request.TaskId);

                if (task == null)
                {
                    var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.TaskNotFound);
                    await Clients.Caller.SendAsync("Error", errorMsg);
                    return;
                }

                var hasAccess = await ValidateTaskAccessAsync(task, userId);
                if (!hasAccess)
                {
                    var errorMsg = await GetLocalizedMessageAsync(
                        task.GroupId.HasValue
                            ? ErrorCodes.GroupPermissionDenied
                            : ErrorCodes.TaskPermissionDenied);
                    await Clients.Caller.SendAsync("Error", errorMsg);
                    return;
                }

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

                var user = await _userRepository.GetByIdAsync(userId);

                var commentDto = new TaskCommentDto
                {
                    CommentId = comment.CommentId,
                    TaskId = comment.TaskId,
                    UserId = comment.UserId,
                    Content = comment.Content,
                    ParentCommentId = null,
                    CreatedAt = comment.CreatedAt,
                    UpdatedAt = comment.UpdatedAt,
                    IsDeleted = comment.IsDeleted,
                    ReplyCount = 0,
                    User = new UserDto
                    {
                        Id = user!.UserId,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        AvatarUrl = user.AvatarUrl
                    }
                };

                await Clients.Group($"task_{request.TaskId}").SendAsync("ReceiveComment", commentDto);

                _logger.LogInformation(
                    "Comment sent to task {TaskId} by user {UserId}",
                    request.TaskId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending comment");
                var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.UnexpectedError);
                await Clients.Caller.SendAsync("Error", errorMsg);
            }
        }

        /// <summary>
        /// Reply t?i comment (threaded conversation)
        /// Validate:
        /// - User ph?i có quy?n access task
        /// - Parent comment ph?i t?n t?i và chýa b? xóa
        /// Broadcast: "CommentReplied" event t?i task room
        /// </summary>
        public async Task ReplyToComment(ReplyToTaskCommentRequest request)
        {
            try
            {
                var userId = GetUserId();
                var task = await _taskRepository.GetByIdAsync(request.TaskId);

                if (task == null)
                {
                    var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.TaskNotFound);
                    await Clients.Caller.SendAsync("Error", errorMsg);
                    return;
                }

                var hasAccess = await ValidateTaskAccessAsync(task, userId);
                if (!hasAccess)
                {
                    var errorMsg = await GetLocalizedMessageAsync(
                        task.GroupId.HasValue
                            ? ErrorCodes.GroupPermissionDenied
                            : ErrorCodes.TaskPermissionDenied);
                    await Clients.Caller.SendAsync("Error", errorMsg);
                    return;
                }

                var parentComment = await _commentRepository.GetByIdAsync(request.ParentCommentId);
                if (parentComment == null || parentComment.IsDeleted)
                {
                    var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.CommentParentNotFound);
                    await Clients.Caller.SendAsync("Error", errorMsg);
                    return;
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

                var replyDto = new TaskCommentDto
                {
                    CommentId = reply.CommentId,
                    TaskId = reply.TaskId,
                    UserId = reply.UserId,
                    Content = reply.Content,
                    ParentCommentId = reply.ParentCommentId,
                    CreatedAt = reply.CreatedAt,
                    UpdatedAt = reply.UpdatedAt,
                    IsDeleted = reply.IsDeleted,
                    ReplyCount = 0,
                    User = new UserDto
                    {
                        Id = user!.UserId,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        AvatarUrl = user.AvatarUrl
                    }
                };

                await Clients.Group($"task_{request.TaskId}").SendAsync("CommentReplied", replyDto);

                _logger.LogInformation(
                    "Reply sent to comment {ParentCommentId} by user {UserId}",
                    request.ParentCommentId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error replying to comment");
                var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.UnexpectedError);
                await Clients.Caller.SendAsync("Error", errorMsg);
            }
        }

        /// <summary>
        /// Xóa comment (soft delete) và t?t c? replies
        /// Validate:
        /// - Comment owner: Có quy?n delete
        /// - Group Owner/Moderator: Có quy?n delete b?t k? comment nào
        /// - Personal task owner: Có quy?n delete b?t k? comment nào
        /// Broadcast: "CommentDeleted" event t?i task room
        /// </summary>
        public async Task DeleteComment(DeleteTaskCommentRequest request)
        {
            try
            {
                var userId = GetUserId();

                var comment = await _commentRepository.GetByIdWithRepliesAsync(request.CommentId);
                if (comment == null)
                {
                    var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.CommentNotFound);
                    await Clients.Caller.SendAsync("Error", errorMsg);
                    return;
                }

                var task = await _taskRepository.GetByIdAsync(comment.TaskId);
                if (task == null)
                {
                    var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.TaskNotFound);
                    await Clients.Caller.SendAsync("Error", errorMsg);
                    return;
                }

                var hasPermission = await ValidateDeletePermissionAsync(comment, task, userId);
                if (!hasPermission)
                {
                    var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.CommentPermissionDenied);
                    await Clients.Caller.SendAsync("Error", errorMsg);
                    return;
                }

                var replyCount = await _commentRepository.GetReplyCountAsync(request.CommentId);

                await _commentRepository.SoftDeleteWithRepliesAsync(request.CommentId);

                await Clients.Group($"task_{comment.TaskId}").SendAsync("CommentDeleted", new
                {
                    CommentId = request.CommentId,
                    TaskId = comment.TaskId,
                    DeletedBy = userId,
                    ReplyCount = replyCount,
                    Timestamp = DateTime.UtcNow
                });

                _logger.LogInformation(
                    "Comment {CommentId} and {ReplyCount} replies deleted by user {UserId}",
                    request.CommentId, replyCount, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting comment");
                var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.UnexpectedError);
                await Clients.Caller.SendAsync("Error", errorMsg);
            }
        }

        /// <summary>
        /// Handle client disconnect
        /// Auto-cleanup: SignalR t? ð?ng remove kh?i groups
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
