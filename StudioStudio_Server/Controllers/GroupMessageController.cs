using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    /// <summary>
    /// Controller for managing Group Messages (message history)
    /// Route: /api/group-messages
    /// Note: Realtime messaging is handled by GroupDiscussHub (SignalR)
    /// </summary>
    [Route("api/group-messages")]
    [ApiController]
    [Authorize]
    public class GroupMessageController(
        IGroupMessageService groupMessageService,
        IMessageService messageService) : ControllerBase
    {
        /// <summary>
        /// Authenticate and get userId from JWT token
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
        /// Get message history in group (pagination)
        /// Validate: User must be member of group
        /// Query:
        /// - Only get parent messages (ParentMessageId = null)
        /// - Include: User info, Replies (1 level only)
        /// - Order by: CreatedAt DESC
        /// - Pagination: offset + limit
        /// Return: List of messages + total count
        /// </summary>
        [HttpGet("{groupId}")]
        public async Task<ActionResult<ApiResponse<GroupMessageListResponse>>> GetGroupMessages(
            Guid groupId,
            [FromQuery] int limit = 100,
            [FromQuery] int offset = 0)
        {
            var userId = ValidateAndGetUserId();
            var result = await groupMessageService.GetGroupMessagesAsync(userId, groupId, limit, offset);
            var message = messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<GroupMessageListResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }
    }
}
