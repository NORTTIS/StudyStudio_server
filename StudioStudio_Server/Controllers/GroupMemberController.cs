using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    /// <summary>
    /// Controller for managing members in Group
    /// Route: /api/group/member
    /// </summary>
    [Route("api/group/member")]
    [ApiController]
    [Authorize]
    public class GroupMemberController : ControllerBase
    {
        private readonly IGroupMemberService _groupMemberService;
        private readonly IMessageService _messageService;

        public GroupMemberController(
            IGroupMemberService groupMemberService,
            IMessageService messageService)
        {
            _groupMemberService = groupMemberService;
            _messageService = messageService;
        }

        /// <summary>
        /// Authenticate and get userId from JWT token
        /// Validate: User must not be admin
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

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null &&
                          bool.TryParse(isAdminClaim, out var adminResult) &&
                          adminResult;

            if (isAdmin)
            {
                throw new AppException(
                    ErrorCodes.AuthForbidden,
                    StatusCodes.Status403Forbidden);
            }

            return userId;
        }

        /// <summary>
        /// [AUTHORIZED] DELETE /api/group/member/remove
        /// Remove member from group
        /// Validate:
        /// - Current user must be Owner or Moderator
        /// - Cannot remove yourself
        /// - Cannot remove Owner
        /// - Moderator cannot remove another Moderator
        /// </summary>
        [HttpDelete("remove")]
        public async Task<ActionResult<ApiResponse<RemoveMemberResponse>>> RemoveMember(
            [FromBody] RemoveMemberRequest request)
        {
            var userId = ValidateAndGetUserId();
            var result = await _groupMemberService.RemoveMemberAsync(userId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessRemoveMember);

            return Ok(ApiResponse<RemoveMemberResponse>.Success(
                ErrorCodes.SuccessRemoveMember,
                message,
                result));
        }

        /// <summary>
        /// [AUTHORIZED] DELETE /api/group/member/{groupId}/leave
        /// Leave a group (self-remove)
        /// Validate:
        /// - User must be a member of the group
        /// - Owner cannot leave
        /// </summary>
        [HttpDelete("{groupId}/leave")]
        public async Task<ActionResult<ApiResponse<LeaveGroupResponse>>> LeaveGroup(Guid groupId)
        {
            var userId = ValidateAndGetUserId();
            var result = await _groupMemberService.LeaveGroupAsync(userId, groupId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessRemoveMember);

            return Ok(ApiResponse<LeaveGroupResponse>.Success(
                ErrorCodes.SuccessRemoveMember,
                message,
                result));
        }

        /// <summary>
        /// [AUTHORIZED] PUT /api/group/member/assign-role
        /// Change member's role in group
        /// Validate:
        /// - Only Owner can assign roles
        /// - Cannot change your own role
        /// - Cannot assign Owner role (only 1 Owner allowed)
        /// - Only 1 Moderator allowed in group
        /// </summary>
        [HttpPut("assign-role")]
        public async Task<ActionResult<ApiResponse<AssignRoleResponse>>> AssignRole(
            [FromBody] AssignRoleRequest request)
        {
            var userId = ValidateAndGetUserId();
            var result = await _groupMemberService.AssignRoleAsync(userId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessAssignRole);

            return Ok(ApiResponse<AssignRoleResponse>.Success(
                ErrorCodes.SuccessAssignRole,
                message,
                result));
        }
    }
}
