using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace StudioStudio_Server.Hubs
{
    /// <summary>
    /// SignalR Hub x? l? realtime Group Discussions (Messages)
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
        private readonly ILogger<GroupDiscussHub> _logger;

        public GroupDiscussHub(
            IGroupMessageRepository messageRepository,
            IGroupParticipantRepository groupParticipantRepository,
            IUserRepository userRepository,
            IMessageService messageService,
            IAnnouncementRepository announcementRepository,
            IUserAnnouncementService userAnnouncementService,
            IGroupRepository groupRepository,
            ILogger<GroupDiscussHub> logger)
        {
            _messageRepository = messageRepository;
            _groupParticipantRepository = groupParticipantRepository;
            _userRepository = userRepository;
            _messageService = messageService;
            _announcementRepository = announcementRepository;
            _userAnnouncementService = userAnnouncementService;
            _groupRepository = groupRepository;
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
        /// Extract user IDs t? @mentions trong message content
        /// Pattern: @{userId} (UUID format)
        /// Example: "Hello @550e8400-e29b-41d4-a716-446655440000"
        /// </summary>
        private List<Guid> ExtractTaggedUserIds(string content)
        {
            var matches = Regex.Matches(content, @"@([a-fA-F0-9\-]{36})");
            return matches.Select(m => Guid.Parse(m.Groups[1].Value)).ToList();
        }

        /// <summary>
        /// Validate user có quy?n delete message không
        /// Message owner: Luôn có quy?n
        /// Group Owner/Moderator: Có quy?n delete b?t k? message nào
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
        /// G?i notification cho users ðý?c @mention
        /// T?o announcement và g?i qua SignalR
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
            var senderName = $"{sender.FirstName} {sender.LastName}";

            var announcement = new Announcement
            {
                AnnouncementId = Guid.NewGuid(),
                Title = await GetLocalizedMessageAsync(ErrorCodes.AnnouncementTagTitle),
                Content = $"{senderName} {await GetLocalizedMessageAsync(ErrorCodes.AnnouncementTagContent)} {group.GroupName}",
                Type = AnnouncementType.Info,
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
                    IsRead = false,
                    CreatedAt = now
                };

                await _userAnnouncementService.AddAnnouncementAsync(userAnnouncement);
                await Clients.User(taggedUserId.ToString()).SendAsync("ReceiveAnnouncement", announcement);
            }

            _logger.LogInformation(
                "Mention notifications sent to {Count} users in group {GroupId}",
                taggedUserIds.Count, groupId);
        }

        /// <summary>
        /// Join group room ð? nh?n realtime messages
        /// Validate: User ph?i là member c?a group
        /// SignalR Group Name: {groupId}
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
        /// Leave group room
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
        /// G?i message m?i vào group (realtime)
        /// Validate: User ph?i là member c?a group
        /// Features: @mentions notification
        /// Broadcast: "ReceiveMessage" event t?i t?t c? members trong group
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
                        AvatarUrl = user.AvatarUrl
                    }
                };

                await Clients.Group(request.GroupId.ToString()).SendAsync("ReceiveMessage", messageDto);

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
        /// Reply t?i message (threaded conversation)
        /// Validate:
        /// - User ph?i là member c?a group
        /// - Parent message ph?i t?n t?i, chýa b? xóa, và thu?c cùng group
        /// Broadcast: "MessageReplied" event t?i group
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
                        AvatarUrl = user.AvatarUrl
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
        /// Xóa message (soft delete) và t?t c? replies
        /// Validate:
        /// - User ph?i là member c?a group
        /// - Message owner: Có quy?n delete
        /// - Group Owner/Moderator: Có quy?n delete b?t k? message nào
        /// Broadcast: "MessageDeleted" event t?i group
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
        /// Auto-cleanup: SignalR t? ð?ng remove kh?i groups
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
