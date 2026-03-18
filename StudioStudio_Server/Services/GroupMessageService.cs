using Microsoft.AspNetCore.Http;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using StudioStudio_Server.Utils;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service handling business logic for Group Messages
    /// Note: Realtime messaging is handled by GroupDiscussHub (SignalR)
    /// This service only handles HTTP queries (message history)
    /// </summary>
    public class GroupMessageService : IGroupMessageService
    {
        private readonly IGroupMessageRepository _messageRepository;
        private readonly IGroupParticipantRepository _groupParticipantRepository;
        private readonly ILogger<GroupMessageService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IActivityLogService _activityLogService;

        public GroupMessageService(
            IGroupMessageRepository messageRepository,
            IGroupParticipantRepository groupParticipantRepository,
            ILogger<GroupMessageService> logger,
            IHttpContextAccessor httpContextAccessor,
            IActivityLogService activityLogService)
        {
            _messageRepository = messageRepository;
            _groupParticipantRepository = groupParticipantRepository;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            _activityLogService = activityLogService;
        }

        /// <summary>
        /// Get message history in group (pagination)
        /// Validate: User must be member of group
        /// Query:
        /// - Condition: GroupId = {groupId} AND IsDeleted = false AND ParentMessageId = null
        /// - Include: User info, Replies (1 level)
        /// - Order by: CreatedAt DESC (newest message first)
        /// - Pagination: Skip({offset}).Take({limit})
        /// </summary>
        public async Task<GroupMessageListResponse> GetGroupMessagesAsync(
            Guid userId,
            Guid groupId,
            int limit,
            int offset)
        {
            await ValidateUserIsGroupMemberAsync(groupId, userId);

            var messages = await _messageRepository.GetByGroupIdAsync(groupId, limit, offset);
            var totalCount = await _messageRepository.GetCountByGroupIdAsync(groupId);

            var messageDtos = messages
                .Select(m => MapToGroupMessageDto(m, includeReplies: true))
                .ToList();

            _logger.LogInformation(
                "Retrieved {Count} messages for group {GroupId} (Total: {Total}). UserId: {UserId}",
                messageDtos.Count, groupId, totalCount, userId);

            return new GroupMessageListResponse
            {
                GroupId = groupId,
                TotalMessages = totalCount,
                Messages = messageDtos
            };
        }

        /// <summary>
        /// Validate user is member of group
        /// </summary>
        private async Task ValidateUserIsGroupMemberAsync(Guid groupId, Guid userId)
        {
            var isUserInGroup = await _groupParticipantRepository
                .IsUserInGroupAsync(groupId, userId);

            if (!isUserInGroup)
            {
                throw new AppException(
                    ErrorCodes.GroupPermissionDenied,
                    StatusCodes.Status403Forbidden);
            }
        }

        /// <summary>
        /// Map GroupMessage entity ? GroupMessageDto
        /// </summary>
        private GroupMessageDto MapToGroupMessageDto(GroupMessage message, bool includeReplies = false)
        {
            var dto = new GroupMessageDto
            {
                MessageId = message.MessageId,
                GroupId = message.GroupId,
                UserId = message.UserId,
                Content = message.Content,
                ParentMessageId = message.ParentMessageId,
                CreatedAt = message.CreatedAt,
                UpdatedAt = message.UpdatedAt,
                IsDeleted = message.IsDeleted,
                User = new UserDto
                {
                    Id = message.User.UserId,
                    FirstName = message.User.FirstName,
                    LastName = message.User.LastName,
                    AvatarUrl = AvatarUrlHelper.BuildAbsoluteAvatarUrl(message.User.AvatarUrl, _httpContextAccessor.HttpContext)
                },
                ReplyCount = message.Replies?.Count(r => !r.IsDeleted) ?? 0,
                Replies = null
            };

            if (includeReplies && message.Replies != null && message.Replies.Any())
            {
                dto.Replies = message.Replies
                    .Where(r => !r.IsDeleted)
                    .Select(r => MapToGroupMessageDto(r, includeReplies: false))
                    .ToList();
            }

            return dto;
        }
    }
}
