using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    [Route("api/group/member")]
    [ApiController]
    public class GroupMemberController : ControllerBase
    {
        private readonly ILogger<GroupMemberController> _logger;
        private readonly IMessageService _messageService;
        private readonly IGroupRepository _groupRepository;
        private readonly IGroupParticipantRepository _groupParticipantRepository;
        private readonly IUserRepository _userRepository;

        public GroupMemberController(
            ILogger<GroupMemberController> logger,
            IMessageService messageService,
            IGroupRepository groupRepository,
            IGroupParticipantRepository groupParticipantRepository,
            IUserRepository userRepository)
        {
            _logger = logger;
            _messageService = messageService;
            _groupRepository = groupRepository;
            _groupParticipantRepository = groupParticipantRepository;
            _userRepository = userRepository;
        }

        [HttpDelete("remove")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<RemoveMemberResponse>>> RemoveMember(
            [FromBody] RemoveMemberRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null && bool.TryParse(isAdminClaim, out var adminResult) && adminResult;
            if (isAdmin)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            // Check if group exists
            var group = await _groupRepository.GetByIdAsync(request.GroupId);
            if (group == null)
            {
                throw new AppException(ErrorCodes.GroupNotFound, StatusCodes.Status404NotFound);
            }

            // Check if current user is Owner or Moderator
            var currentUserParticipant = await _groupParticipantRepository.GetByGroupAndUserAsync(request.GroupId, userId);
            if (currentUserParticipant == null ||
                (currentUserParticipant.Role != GroupRole.Owner && currentUserParticipant.Role != GroupRole.Moderator))
            {
                throw new AppException(ErrorCodes.GroupPermissionDenied, StatusCodes.Status403Forbidden);
            }

            // Cannot remove yourself
            if (request.UserId == userId)
            {
                throw new AppException(ErrorCodes.GroupCannotRemoveSelf, StatusCodes.Status400BadRequest);
            }

            // Get target member
            var targetMember = await _groupParticipantRepository.GetByGroupAndUserAsync(request.GroupId, request.UserId);
            if (targetMember == null)
            {
                throw new AppException(ErrorCodes.GroupMemberNotFound, StatusCodes.Status404NotFound);
            }

            // Cannot remove Owner
            if (targetMember.Role == GroupRole.Owner)
            {
                throw new AppException(ErrorCodes.GroupCannotRemoveOwner, StatusCodes.Status400BadRequest);
            }

            // Moderators can only remove Members, Commenters, and Viewers (not other Moderators)
            if (currentUserParticipant.Role == GroupRole.Moderator && targetMember.Role == GroupRole.Moderator)
            {
                throw new AppException(ErrorCodes.GroupPermissionDenied, StatusCodes.Status403Forbidden);
            }

            // Get removed user info
            var removedUser = await _userRepository.GetByIdAsync(request.UserId);
            if (removedUser == null)
            {
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
            }

            // Remove member
            await _groupParticipantRepository.RemoveAsync(targetMember);

            _logger.LogInformation(
                "User {UserId} removed user {RemovedUserId} from group {GroupId}",
                userId, request.UserId, request.GroupId);

            var response = new RemoveMemberResponse
            {
                GroupId = group.GroupId,
                GroupName = group.GroupName,
                RemovedUserId = request.UserId,
                RemovedUserName = $"{removedUser.FirstName} {removedUser.LastName}",
                RemovedAt = DateTime.UtcNow
            };

            var message = _messageService.GetMessage(ErrorCodes.SuccessRemoveMember);
            return Ok(ApiResponse<RemoveMemberResponse>.Success(ErrorCodes.SuccessRemoveMember, message, response));
        }

        [HttpPut("assign-role")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<AssignRoleResponse>>> AssignRole(
            [FromBody] AssignRoleRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null && bool.TryParse(isAdminClaim, out var adminResult) && adminResult;
            if (isAdmin)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            // Validate role
            if (!Enum.TryParse<GroupRole>(request.Role, true, out GroupRole newRole))
            {
                throw new AppException(ErrorCodes.InviteInvalidRole, StatusCodes.Status400BadRequest);
            }

            // Check if group exists
            var group = await _groupRepository.GetByIdAsync(request.GroupId);
            if (group == null)
            {
                throw new AppException(ErrorCodes.GroupNotFound, StatusCodes.Status404NotFound);
            }

            // Only Owner can assign roles
            var currentUserParticipant = await _groupParticipantRepository.GetByGroupAndUserAsync(request.GroupId, userId);
            if (currentUserParticipant == null || currentUserParticipant.Role != GroupRole.Owner)
            {
                throw new AppException(ErrorCodes.GroupPermissionDenied, StatusCodes.Status403Forbidden);
            }

            // Cannot change your own role
            if (request.UserId == userId)
            {
                throw new AppException(ErrorCodes.GroupCannotChangeOwnRole, StatusCodes.Status400BadRequest);
            }

            // Get target member
            var targetMember = await _groupParticipantRepository.GetByGroupAndUserAsync(request.GroupId, request.UserId);
            if (targetMember == null)
            {
                throw new AppException(ErrorCodes.GroupMemberNotFound, StatusCodes.Status404NotFound);
            }

            var oldRole = targetMember.Role;

            // Only one Owner allowed
            if (newRole == GroupRole.Owner)
            {
                throw new AppException(ErrorCodes.GroupOnlyOneOwner, StatusCodes.Status400BadRequest);
            }

            // Only one Moderator allowed
            if (newRole == GroupRole.Moderator)
            {
                int moderatorCount = await _groupParticipantRepository.GetRoleCountByGroupIdAsync(request.GroupId, GroupRole.Moderator);
                if (moderatorCount > 0 && targetMember.Role != GroupRole.Moderator)
                {
                    throw new AppException(ErrorCodes.GroupOnlyOneModerator, StatusCodes.Status400BadRequest);
                }
            }

            // Update role
            targetMember.Role = newRole;
            await _groupParticipantRepository.UpdateAsync(targetMember);

            // Get user info
            var user = await _userRepository.GetByIdAsync(request.UserId);
            if (user == null)
            {
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
            }

            _logger.LogInformation(
                "User {UserId} changed role of user {TargetUserId} from {OldRole} to {NewRole} in group {GroupId}",
                userId, request.UserId, oldRole, newRole, request.GroupId);

            var response = new AssignRoleResponse
            {
                GroupId = group.GroupId,
                GroupName = group.GroupName,
                UserId = request.UserId,
                UserName = $"{user.FirstName} {user.LastName}",
                OldRole = oldRole.ToString(),
                NewRole = newRole.ToString(),
                UpdatedAt = DateTime.UtcNow
            };

            var message = _messageService.GetMessage(ErrorCodes.SuccessAssignRole);
            return Ok(ApiResponse<AssignRoleResponse>.Success(ErrorCodes.SuccessAssignRole, message, response));
        }
    }
}
