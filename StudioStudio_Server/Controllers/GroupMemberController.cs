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
    /// Controller qu?n l? members trong Group
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
        /// Xác th?c và l?y userId t? JWT token
        /// Validate: User không ðý?c là admin
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
        /// Remove member kh?i group
        /// Validate:
        /// - Current user ph?i là Owner ho?c Moderator
        /// - Không th? remove chính m?nh
        /// - Không th? remove Owner
        /// - Moderator không th? remove Moderator khác
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
        /// [AUTHORIZED] PUT /api/group/member/assign-role
        /// Thay ð?i role c?a member trong group
        /// Validate:
        /// - Ch? Owner m?i có quy?n assign role
        /// - Không th? ð?i role c?a chính m?nh
        /// - Không th? assign role Owner (ch? có 1 Owner)
        /// - Ch? có th? có 1 Moderator trong group
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
