using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    /// <summary>
    /// Controller qu?n l? Group Messages (l?ch s? tin nh?n)
    /// Route: /api/group-messages
    /// Note: Realtime messaging ðý?c handle b?i GroupDiscussHub (SignalR)
    /// </summary>
    [Route("api/group-messages")]
    [ApiController]
    [Authorize]
    public class GroupMessageController : ControllerBase
    {
        private readonly IGroupMessageService _groupMessageService;
        private readonly IMessageService _messageService;

        public GroupMessageController(
            IGroupMessageService groupMessageService,
            IMessageService messageService)
        {
            _groupMessageService = groupMessageService;
            _messageService = messageService;
        }

        /// <summary>
        /// Xác th?c và l?y userId t? JWT token
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
        /// [AUTHORIZED] GET /api/group-messages/{groupId}?limit=100&offset=0
        /// L?y l?ch s? tin nh?n trong group (pagination)
        /// Validate: User ph?i là member c?a group
        /// Query:
        /// - Ch? l?y parent messages (ParentMessageId = null)
        /// - Include: User info, Replies (1 level only)
        /// - S?p x?p: CreatedAt DESC
        /// - Pagination: offset + limit
        /// Return: Danh sách messages + total count
        /// </summary>
        [HttpGet("{groupId}")]
        public async Task<ActionResult<ApiResponse<GroupMessageListResponse>>> GetGroupMessages(
            Guid groupId,
            [FromQuery] int limit = 100,
            [FromQuery] int offset = 0)
        {
            var userId = ValidateAndGetUserId();
            var result = await _groupMessageService.GetGroupMessagesAsync(userId, groupId, limit, offset);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<GroupMessageListResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }
    }
}
