using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Metrics;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using StudioStudio_Server.Utils;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Text.RegularExpressions;

namespace StudioStudio_Server.Hubs
{
    /// <summary>
    /// SignalR Hub handling realtime Group Discussions (Messages)
    /// Route: /hubs/group-discuss
    /// Features: Join/Leave group, Send message, Reply to message, Delete message, @mentions
    /// </summary>
    [Authorize]
    public class GroupDiscussHub : Hub
    {
        private readonly IGroupMessageRepository _messageRepository;
        private readonly IGroupParticipantRepository _groupParticipantRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMessageService _messageService;
        private readonly IAnnouncementRepository _announcementRepository;
        private readonly IUserAnnouncementService _userAnnouncementService;
        private readonly IGroupRepository _groupRepository;
        private readonly INotificationService _notificationService;
        private readonly ILogger<GroupDiscussHub> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IActivityLogService _activityLogService;

        public GroupDiscussHub(
            IGroupMessageRepository messageRepository,
            IGroupParticipantRepository groupParticipantRepository,
            IUserRepository userRepository,
            IMessageService messageService,
            IAnnouncementRepository announcementRepository,
            IUserAnnouncementService userAnnouncementService,
            IGroupRepository groupRepository,
            INotificationService notificationService,
            ILogger<GroupDiscussHub> logger,
            IHttpContextAccessor httpContextAccessor,
            IActivityLogService activityLogService)
        {
            _messageRepository = messageRepository;
            _groupParticipantRepository = groupParticipantRepository;
            _userRepository = userRepository;
            _messageService = messageService;
            _announcementRepository = announcementRepository;
            _userAnnouncementService = userAnnouncementService;
            _groupRepository = groupRepository;
            _notificationService = notificationService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _activityLogService = activityLogService;
        }

        /// <summary>
        /// Get userId from SignalR Context
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
        /// Get localized error message according to user's language
        /// </summary>
        private async Task<string> GetLocalizedMessageAsync(string errorCode)
        {
            try
            {
                var userId = GetUserId();
                var user = await _userRepository.GetByIdAsync(userId);

                if (user != null && !string.IsNullOrEmpty(user.Language))
                {
                    var httpContext = Context.GetHttpContext();
                    if (httpContext != null)
                    {
                        httpContext.Request.Headers["Accept-Language"] = user.Language;
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
        /// Extract user IDs from @mentions in message content
        /// Pattern: @{userId} (UUID format)
        /// Example: "Hello @550e8400-e29b-41d4-a716-446655440000"
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
        /// Validate delete permission
        /// Rules:
        /// - Message owner: Can delete
        /// - Group Owner/Moderator: Can delete any message
        /// </summary>
        private async Task<bool> ValidateDeletePermissionAsync(GroupMessage message, Guid userId)
        {
            if (message.UserId == userId)
            {
                return true;
            }

            var participant = await _groupParticipantRepository
                .GetByUserAndGroupAsync(userId, message.GroupId);

            return participant != null &&
                   (participant.Role == GroupRole.Owner ||
                    participant.Role == GroupRole.Moderator);
        }

        /// <summary>
        /// Handle @mention notifications
        /// Create UserAnnouncement for tagged users
        /// </summary>
        private async Task HandleMentionNotificationsAsync(
            Guid groupId,
            Guid senderId,
            string content)
        {
            var taggedUserIds = ExtractTaggedUserIds(content);

            if (taggedUserIds.IsNullOrEmpty())
            {
                return;
            }

            var now = DateTime.UtcNow;
            var sender = await _userRepository.GetByIdAsync(senderId);
            var group = await _groupRepository.GetByIdAsync(groupId);

            if (sender == null || group == null)
            {
                _logger.LogWarning("Cannot send mention notification: sender={SenderId} or group={GroupId} not found",
                    senderId, groupId);
                return;
            }

            var senderName = $"{sender.FirstName} {sender.LastName}";

            var announcement = new Announcement
            {
                AnnouncementId = Guid.NewGuid(),
                Title = await GetLocalizedMessageAsync(ErrorCodes.AnnouncementTagTitle),
                Content = $"{senderName} {await GetLocalizedMessageAsync(ErrorCodes.AnnouncementTagContent)} {group.GroupName} - {ExtractPlainText(content)}",
                Type = AnnouncementType.Mention,
                IsActive = true,
                CreatedBy = senderId,
                CreatedAt = now,
                UpdatedAt = now,
                PublishedAt = now
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
                await _notificationService.NotifyMentionedInGroupDiscussAsync(
                    taggedUserId,
                    groupId,
                    senderId,
                    group.GroupName,
                    ExtractPlainText(content));
                await Clients.User(taggedUserId.ToString()).SendAsync("ReceiveAnnouncement", announcement);
            }

            _logger.LogInformation(
                "Mention notifications sent to {Count} users in group {GroupId}",
                taggedUserIds.Count, groupId);
        }

        /// <summary>
        /// Join group discussion room
        /// Validate: User must be member of group
        /// Action: Add connection to SignalR group
        /// </summary>
        public async Task JoinGroup(Guid groupId)
        {
            try
            {
                var userId = GetUserId();

                var isUserInGroup = await _groupParticipantRepository.IsUserInGroupAsync(groupId, userId);
                if (!isUserInGroup)
                {
                    var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.GroupPermissionDenied);
                    await Clients.Caller.SendAsync("Error", errorMsg);
                    return;
                }

                await Groups.AddToGroupAsync(Context.ConnectionId, groupId.ToString());
                _logger.LogInformation("User {UserId} joined group {GroupId}", userId, groupId);

                await Clients.Group(groupId.ToString()).SendAsync("UserJoined", new
                {
                    UserId = userId,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining group");
                var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.UnexpectedError);
                await Clients.Caller.SendAsync("Error", errorMsg);
            }
        }

        /// <summary>
        /// Leave group discussion room
        /// Action: Remove connection from SignalR group
        /// </summary>
        public async Task LeaveGroup(Guid groupId)
        {
            try
            {
                var userId = GetUserId();

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupId.ToString());
                _logger.LogInformation("User {UserId} left group {GroupId}", userId, groupId);

                await Clients.Group(groupId.ToString()).SendAsync("UserLeft", new
                {
                    UserId = userId,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving group");
                var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.UnexpectedError);
                await Clients.Caller.SendAsync("Error", errorMsg);
            }
        }

        /// <summary>
        /// Send message to group
        /// Validate: User must be member of group
        /// Action:
        /// - Save message to database
        /// - Broadcast to all group members
        /// - Handle @mentions notifications
        /// </summary>
        public async Task SendMessage(SendGroupMessageRequest request)
        {
            try
            {
                var userId = GetUserId();

                var isUserInGroup = await _groupParticipantRepository.IsUserInGroupAsync(request.GroupId, userId);
                if (!isUserInGroup)
                {
                    var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.GroupPermissionDenied);
                    await Clients.Caller.SendAsync("Error", errorMsg);
                    return;
                }

                var message = new GroupMessage
                {
                    MessageId = Guid.NewGuid(),
                    GroupId = request.GroupId,
                    UserId = userId,
                    Content = request.Content,
                    ParentMessageId = null,
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };

                await _messageRepository.AddAsync(message);

                // Log message creation activity
                var group = await _groupRepository.GetByIdAsync(request.GroupId);
                await _activityLogService.LogMessageCreateAsync(userId, message.MessageId, request.GroupId, group?.StudioId);

                var user = await _userRepository.GetByIdAsync(userId);

                var messageDto = new GroupMessageDto
                {
                    MessageId = message.MessageId,
                    GroupId = message.GroupId,
                    UserId = message.UserId,
                    Content = message.Content,
                    ParentMessageId = null,
                    CreatedAt = message.CreatedAt,
                    UpdatedAt = message.UpdatedAt,
                    IsDeleted = message.IsDeleted,
                    ReplyCount = 0,
                    User = new UserDto
                    {
                        Id = user!.UserId,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        AvatarUrl = AvatarUrlHelper.BuildAbsoluteAvatarUrl(user.AvatarUrl, _httpContextAccessor.HttpContext)
                    }
                };

                await Clients.Group(request.GroupId.ToString()).SendAsync("ReceiveMessage", messageDto);

                // Record SignalR message metric
                AppMetrics.SignalRMessagesTotal.WithLabels("group-discuss").Inc();

                _logger.LogInformation(
                    "Message sent to group {GroupId} by user {UserId}",
                    request.GroupId, userId);

                await HandleMentionNotificationsAsync(request.GroupId, userId, request.Content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
                var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.UnexpectedError);
                await Clients.Caller.SendAsync("Error", errorMsg);
            }
        }

        /// <summary>
        /// Reply to message
        /// Validate:
        /// - User must be member of group
        /// - Parent message must exist
        /// Broadcast: "MessageReplied" event to group
        /// </summary>
        public async Task ReplyToMessage(ReplyToGroupMessageRequest request)
        {
            try
            {
                var userId = GetUserId();

                _logger.LogInformation(
                    "ReplyToMessage - Request: GroupId={GroupId}, ParentMessageId={ParentId}, UserId={UserId}, Content length={Length}",
                    request.GroupId, request.ParentMessageId, userId, request.Content?.Length ?? 0);

                var isUserInGroup = await _groupParticipantRepository.IsUserInGroupAsync(request.GroupId, userId);
                if (!isUserInGroup)
                {
                    _logger.LogWarning("User {UserId} not authorized for group {GroupId}", userId, request.GroupId);
                    var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.GroupPermissionDenied);
                    await Clients.Caller.SendAsync("Error", errorMsg);
                    return;
                }

                var parentMessage = await _messageRepository.GetByIdAsync(request.ParentMessageId);

                _logger.LogInformation(
                    "Parent message check: Found={Found}, IsDeleted={IsDeleted}, GroupId={GroupId}",
                    parentMessage != null, parentMessage?.IsDeleted, parentMessage?.GroupId);

                if (parentMessage == null || parentMessage.IsDeleted)
                {
                    _logger.LogError(
                        "Parent message not found or deleted: ParentMessageId={ParentMessageId}",
                        request.ParentMessageId);
                    var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.MessageParentNotFound);
                    await Clients.Caller.SendAsync("Error", errorMsg);
                    return;
                }

                if (parentMessage.GroupId != request.GroupId)
                {
                    _logger.LogError(
                        "Parent message GroupId mismatch: ParentGroupId={ParentGroupId}, RequestGroupId={RequestGroupId}",
                        parentMessage.GroupId, request.GroupId);
                    var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.MessageParentNotFound);
                    await Clients.Caller.SendAsync("Error", errorMsg);
                    return;
                }

                var reply = new GroupMessage
                {
                    MessageId = Guid.NewGuid(),
                    GroupId = request.GroupId,
                    UserId = userId,
                    Content = request.Content,
                    ParentMessageId = request.ParentMessageId,
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };

                _logger.LogInformation(
                    "Attempting to save reply: ReplyId={ReplyId}, ParentId={ParentId}, GroupId={GroupId}",
                    reply.MessageId, reply.ParentMessageId, reply.GroupId);

                await _messageRepository.AddAsync(reply);

                _logger.LogInformation("Reply saved successfully: ReplyId={ReplyId}", reply.MessageId);

                var user = await _userRepository.GetByIdAsync(userId);

                var replyDto = new GroupMessageDto
                {
                    MessageId = reply.MessageId,
                    GroupId = reply.GroupId,
                    UserId = reply.UserId,
                    Content = reply.Content,
                    ParentMessageId = reply.ParentMessageId,
                    CreatedAt = reply.CreatedAt,
                    UpdatedAt = reply.UpdatedAt,
                    IsDeleted = reply.IsDeleted,
                    ReplyCount = 0,
                    User = new UserDto
                    {
                        Id = user!.UserId,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        AvatarUrl = AvatarUrlHelper.BuildAbsoluteAvatarUrl(user.AvatarUrl, _httpContextAccessor.HttpContext)
                    }
                };

                await Clients.Group(request.GroupId.ToString()).SendAsync("MessageReplied", replyDto);

                _logger.LogInformation(
                    "Reply broadcasted to group {GroupId} for message {ParentMessageId}",
                    request.GroupId, request.ParentMessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error replying to message - GroupId={GroupId}, ParentMessageId={ParentMessageId}",
                    request.GroupId, request.ParentMessageId);
                var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.UnexpectedError);
                await Clients.Caller.SendAsync("Error", errorMsg);
            }
        }

        /// <summary>
        /// Delete message (soft delete) and all replies
        /// Validate:
        /// - User must be member of group
        /// - Message owner: Has delete permission
        /// - Group Owner/Moderator: Has delete permission for any message
        /// Broadcast: "MessageDeleted" event to group
        /// </summary>
        public async Task DeleteMessage(DeleteGroupMessageRequest request)
        {
            try
            {
                var userId = GetUserId();

                var message = await _messageRepository.GetByIdWithRepliesAsync(request.MessageId);
                if (message == null)
                {
                    var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.MessageNotFound);
                    await Clients.Caller.SendAsync("Error", errorMsg);
                    return;
                }

                var isUserInGroup = await _groupParticipantRepository.IsUserInGroupAsync(message.GroupId, userId);
                if (!isUserInGroup)
                {
                    var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.GroupPermissionDenied);
                    await Clients.Caller.SendAsync("Error", errorMsg);
                    return;
                }

                var hasPermission = await ValidateDeletePermissionAsync(message, userId);
                if (!hasPermission)
                {
                    var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.MessagePermissionDenied);
                    await Clients.Caller.SendAsync("Error", errorMsg);
                    return;
                }

                var replyCount = await _messageRepository.GetReplyCountAsync(request.MessageId);

                await _messageRepository.SoftDeleteWithRepliesAsync(request.MessageId);

                await Clients.Group(message.GroupId.ToString()).SendAsync("MessageDeleted", new
                {
                    MessageId = request.MessageId,
                    GroupId = message.GroupId,
                    DeletedBy = userId,
                    ReplyCount = replyCount,
                    Timestamp = DateTime.UtcNow
                });

                _logger.LogInformation(
                    "Message {MessageId} and {ReplyCount} replies deleted by user {UserId}",
                    request.MessageId, replyCount, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message");
                var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.UnexpectedError);
                await Clients.Caller.SendAsync("Error", errorMsg);
            }
        }

        /// <summary>
        /// Handle client disconnect
        /// Auto-cleanup: SignalR automatically removes from groups
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);

            // Update SignalR connection metrics
            AppMetrics.SignalRConnections.WithLabels("group-discuss").Dec();

            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Handle client connection
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            // Update SignalR connection metrics
            AppMetrics.SignalRConnections.WithLabels("group-discuss").Inc();

            await base.OnConnectedAsync();
        }
    }
}
