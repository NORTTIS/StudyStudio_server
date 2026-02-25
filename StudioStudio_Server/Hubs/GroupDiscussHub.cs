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
    [Authorize]
    public class GroupDiscussHub : Hub
    {
        private readonly IGroupMessageRepository _messageRepository;
        private readonly IGroupParticipantRepository _groupParticipantRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMessageService _messageService;
        private readonly ILogger<GroupDiscussHub> _logger;

        public GroupDiscussHub(
            IGroupMessageRepository messageRepository,
            IGroupParticipantRepository groupParticipantRepository,
            IUserRepository userRepository,
            IMessageService messageService,
            ILogger<GroupDiscussHub> logger)
        {
            _messageRepository = messageRepository;
            _groupParticipantRepository = groupParticipantRepository;
            _userRepository = userRepository;
            _messageService = messageService;
            _logger = logger;
        }

        private Guid GetUserId()
        {
            var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }
            return userId;
        }

        private async Task<string> GetLocalizedMessageAsync(string errorCode)
        {
            try
            {
                var userId = GetUserId();
                var user = await _userRepository.GetByIdAsync(userId);
                
                // If user has language preference, use it
                if (user != null && !string.IsNullOrEmpty(user.Language))
                {
                    // Temporarily set language in HTTP context if available
                    if (Context.GetHttpContext() != null)
                    {
                        Context.GetHttpContext().Request.Headers["Accept-Language"] = user.Language;
                    }
                }
                
                return _messageService.GetMessage(errorCode);
            }
            catch
            {
                // Fallback to default message service behavior
                return _messageService.GetMessage(errorCode);
            }
        }

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
                _logger.LogInformation("Message sent to group {GroupId} by user {UserId}", request.GroupId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending message");
                var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.UnexpectedError);
                await Clients.Caller.SendAsync("Error", errorMsg);
            }
        }

        public async Task ReplyToMessage(ReplyToGroupMessageRequest request)
        {
            try
            {
                var userId = GetUserId();

                // ? LOG: Request data
                _logger.LogInformation("ReplyToMessage - Request: GroupId={GroupId}, ParentMessageId={ParentId}, UserId={UserId}, Content length={Length}", 
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
                
                // ? LOG: Parent message status
                _logger.LogInformation("Parent message check: Found={Found}, IsDeleted={IsDeleted}, GroupId={GroupId}", 
                    parentMessage != null, parentMessage?.IsDeleted, parentMessage?.GroupId);
                
                if (parentMessage == null || parentMessage.IsDeleted)
                {
                    _logger.LogError("Parent message not found or deleted: ParentMessageId={ParentMessageId}", request.ParentMessageId);
                    var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.MessageParentNotFound);
                    await Clients.Caller.SendAsync("Error", errorMsg);
                    return;
                }

                // ? VALIDATION: Verify parent belongs to same group
                if (parentMessage.GroupId != request.GroupId)
                {
                    _logger.LogError("Parent message GroupId mismatch: ParentGroupId={ParentGroupId}, RequestGroupId={RequestGroupId}", 
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

                // ? LOG: Before save
                _logger.LogInformation("Attempting to save reply: ReplyId={ReplyId}, ParentId={ParentId}, GroupId={GroupId}", 
                    reply.MessageId, reply.ParentMessageId, reply.GroupId);

                await _messageRepository.AddAsync(reply);

                // ? LOG: After save
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
                _logger.LogInformation("Reply broadcasted to group {GroupId} for message {ParentMessageId}", request.GroupId, request.ParentMessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error replying to message - GroupId={GroupId}, ParentMessageId={ParentMessageId}, Error={Error}", 
                    request.GroupId, request.ParentMessageId, ex.Message);
                var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.UnexpectedError);
                await Clients.Caller.SendAsync("Error", errorMsg);
            }
        }

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

                if (message.UserId != userId)
                {
                    var participant = await _groupParticipantRepository.GetByUserAndGroupAsync(userId, message.GroupId);
                    if (participant == null || (participant.Role != GroupRole.Owner && participant.Role != GroupRole.Moderator))
                    {
                        var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.MessagePermissionDenied);
                        await Clients.Caller.SendAsync("Error", errorMsg);
                        return;
                    }
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

                _logger.LogInformation("Message {MessageId} and {ReplyCount} replies deleted by user {UserId}", request.MessageId, replyCount, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message");
                var errorMsg = await GetLocalizedMessageAsync(ErrorCodes.UnexpectedError);
                await Clients.Caller.SendAsync("Error", errorMsg);
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
