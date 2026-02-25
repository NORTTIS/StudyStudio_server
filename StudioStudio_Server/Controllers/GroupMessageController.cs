using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Repositories.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    [Route("api/group-messages")]
    [ApiController]
    [Authorize]
    public class GroupMessageController : ControllerBase
    {
        private readonly IGroupMessageRepository _messageRepository;
        private readonly IGroupParticipantRepository _groupParticipantRepository;
        private readonly ILogger<GroupMessageController> _logger;

        public GroupMessageController(
            IGroupMessageRepository messageRepository,
            IGroupParticipantRepository groupParticipantRepository,
            ILogger<GroupMessageController> logger)
        {
            _messageRepository = messageRepository;
            _groupParticipantRepository = groupParticipantRepository;
            _logger = logger;
        }

        [HttpGet("{groupId}")]
        public async Task<ActionResult<ApiResponse<GroupMessageListResponse>>> GetGroupMessages(
            Guid groupId,
            [FromQuery] int limit = 100,
            [FromQuery] int offset = 0)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            var isUserInGroup = await _groupParticipantRepository.IsUserInGroupAsync(groupId, userId);
            if (!isUserInGroup)
            {
                throw new AppException(ErrorCodes.GroupPermissionDenied, StatusCodes.Status403Forbidden);
            }

            var messages = await _messageRepository.GetByGroupIdAsync(groupId, limit, offset);
            var totalCount = await _messageRepository.GetCountByGroupIdAsync(groupId);

            var messageDtos = messages.Select(m => new GroupMessageDto
            {
                MessageId = m.MessageId,
                GroupId = m.GroupId,
                UserId = m.UserId,
                Content = m.Content,
                ParentMessageId = m.ParentMessageId,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt,
                IsDeleted = m.IsDeleted,
                User = new UserDto
                {
                    Id = m.User.UserId,
                    FirstName = m.User.FirstName,
                    LastName = m.User.LastName,
                    AvatarUrl = m.User.AvatarUrl
                },
                ReplyCount = m.Replies?.Count(r => !r.IsDeleted) ?? 0,
                Replies = m.Replies?
                    .Where(r => !r.IsDeleted)
                    .Select(r => new GroupMessageDto
                    {
                        MessageId = r.MessageId,
                        GroupId = r.GroupId,
                        UserId = r.UserId,
                        Content = r.Content,
                        ParentMessageId = r.ParentMessageId,
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
            }).ToList();

            var response = new GroupMessageListResponse
            {
                GroupId = groupId,
                TotalMessages = totalCount,
                Messages = messageDtos
            };

            return Ok(ApiResponse<GroupMessageListResponse>.Success(
                ErrorCodes.SuccessGetData,
                "Messages retrieved successfully",
                response));
        }
    }
}
