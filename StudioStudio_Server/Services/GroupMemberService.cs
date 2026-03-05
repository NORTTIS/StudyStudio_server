using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service for handling business logic for Group Members
    /// </summary>
    public class GroupMemberService : IGroupMemberService
    {
        private readonly IGroupRepository _groupRepository;
        private readonly IGroupParticipantRepository _groupParticipantRepository;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<GroupMemberService> _logger;

        public GroupMemberService(
            IGroupRepository groupRepository,
            IGroupParticipantRepository groupParticipantRepository,
            IUserRepository userRepository,
            ILogger<GroupMemberService> logger)
        {
            _groupRepository = groupRepository;
            _groupParticipantRepository = groupParticipantRepository;
            _userRepository = userRepository;
            _logger = logger;
        }

        /// <summary>
        /// Remove member from group
        /// Validate:
        /// - Group must exist
        /// - Current user must be Owner or Moderator
        /// - Cannot remove yourself
        /// - Cannot remove Owner
        /// - Moderator can only remove Member/Commenter/Viewer (cannot remove another Moderator)
        /// </summary>
        public async Task<RemoveMemberResponse> RemoveMemberAsync(
            Guid currentUserId,
            RemoveMemberRequest request)
        {
            var group = await ValidateGroupExistsAsync(request.GroupId);

            var currentUserParticipant = await ValidateUserIsModeratorOrOwnerAsync(
                request.GroupId,
                currentUserId);

            if (request.UserId == currentUserId)
            {
                throw new AppException(
                    ErrorCodes.GroupCannotRemoveSelf,
                    StatusCodes.Status400BadRequest);
            }

            var targetMember = await _groupParticipantRepository
                .GetByGroupAndUserTrackedAsync(request.GroupId, request.UserId);

            if (targetMember == null)
            {
                throw new AppException(
                    ErrorCodes.GroupMemberNotFound,
                    StatusCodes.Status404NotFound);
            }

            if (targetMember.Role == GroupRole.Owner)
            {
                throw new AppException(
                    ErrorCodes.GroupCannotRemoveOwner,
                    StatusCodes.Status400BadRequest);
            }

            if (currentUserParticipant.Role == GroupRole.Moderator &&
                targetMember.Role == GroupRole.Moderator)
            {
                throw new AppException(
                    ErrorCodes.GroupPermissionDenied,
                    StatusCodes.Status403Forbidden);
            }

            var removedUser = await GetUserOrThrowAsync(request.UserId);

            await _groupParticipantRepository.RemoveAsync(targetMember);

            _logger.LogInformation(
                "User {UserId} removed user {RemovedUserId} from group {GroupId}",
                currentUserId, request.UserId, request.GroupId);

            return new RemoveMemberResponse
            {
                GroupId = group.GroupId,
                GroupName = group.GroupName,
                RemovedUserId = request.UserId,
                RemovedUserName = $"{removedUser.FirstName} {removedUser.LastName}",
                RemovedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Assign (change) role of member in group
        /// Validate:
        /// - Group must exist
        /// - Only Owner can assign roles
        /// - Cannot change your own role
        /// - Cannot assign Owner role (only 1 Owner allowed)
        /// - Only 1 Moderator allowed
        /// </summary>
        public async Task<AssignRoleResponse> AssignRoleAsync(
            Guid currentUserId,
            AssignRoleRequest request)
        {
            if (!Enum.TryParse<GroupRole>(request.Role, true, out GroupRole newRole))
            {
                throw new AppException(
                    ErrorCodes.InviteInvalidRole,
                    StatusCodes.Status400BadRequest);
            }

            var group = await ValidateGroupExistsAsync(request.GroupId);

            var currentUserParticipant = await ValidateUserIsOwnerAsync(
                request.GroupId,
                currentUserId);

            if (request.UserId == currentUserId)
            {
                throw new AppException(
                    ErrorCodes.GroupCannotChangeOwnRole,
                    StatusCodes.Status400BadRequest);
            }

            var targetMember = await _groupParticipantRepository
                .GetByGroupAndUserTrackedAsync(request.GroupId, request.UserId);

            if (targetMember == null)
            {
                throw new AppException(
                    ErrorCodes.GroupMemberNotFound,
                    StatusCodes.Status404NotFound);
            }

            var oldRole = targetMember.Role;

            if (newRole == GroupRole.Owner)
            {
                throw new AppException(
                    ErrorCodes.GroupOnlyOneOwner,
                    StatusCodes.Status400BadRequest);
            }

            if (newRole == GroupRole.Moderator)
            {
                int moderatorCount = await _groupParticipantRepository
                    .GetRoleCountByGroupIdAsync(request.GroupId, GroupRole.Moderator);

                if (moderatorCount > 0 && targetMember.Role != GroupRole.Moderator)
                {
                    throw new AppException(
                        ErrorCodes.GroupOnlyOneModerator,
                        StatusCodes.Status400BadRequest);
                }
            }

            targetMember.Role = newRole;
            await _groupParticipantRepository.UpdateAsync(targetMember);

            var user = await GetUserOrThrowAsync(request.UserId);

            _logger.LogInformation(
                "User {UserId} changed role of user {TargetUserId} from {OldRole} to {NewRole} in group {GroupId}",
                currentUserId, request.UserId, oldRole, newRole, request.GroupId);

            return new AssignRoleResponse
            {
                GroupId = group.GroupId,
                GroupName = group.GroupName,
                UserId = request.UserId,
                UserName = $"{user.FirstName} {user.LastName}",
                OldRole = oldRole.ToString(),
                NewRole = newRole.ToString(),
                UpdatedAt = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Validate group exists
        /// </summary>
        private async Task<Group> ValidateGroupExistsAsync(Guid groupId)
        {
            var group = await _groupRepository.GetByIdAsync(groupId);

            if (group == null)
            {
                throw new AppException(
                    ErrorCodes.GroupNotFound,
                    StatusCodes.Status404NotFound);
            }

            return group;
        }

        /// <summary>
        /// Validate user is Owner or Moderator of group
        /// </summary>
        private async Task<GroupParticipant> ValidateUserIsModeratorOrOwnerAsync(
            Guid groupId,
            Guid userId)
        {
            var participant = await _groupParticipantRepository
                .GetByGroupAndUserAsync(groupId, userId);

            if (participant == null ||
                (participant.Role != GroupRole.Owner &&
                 participant.Role != GroupRole.Moderator))
            {
                throw new AppException(
                    ErrorCodes.GroupPermissionDenied,
                    StatusCodes.Status403Forbidden);
            }

            return participant;
        }

        /// <summary>
        /// Validate user is Owner of group
        /// </summary>
        private async Task<GroupParticipant> ValidateUserIsOwnerAsync(
            Guid groupId,
            Guid userId)
        {
            var participant = await _groupParticipantRepository
                .GetByGroupAndUserAsync(groupId, userId);

            if (participant == null || participant.Role != GroupRole.Owner)
            {
                throw new AppException(
                    ErrorCodes.GroupPermissionDenied,
                    StatusCodes.Status403Forbidden);
            }

            return participant;
        }

        /// <summary>
        /// Get GroupParticipant or throw exception
        /// </summary>
        private async Task<GroupParticipant> GetParticipantOrThrowAsync(
            Guid groupId,
            Guid userId)
        {
            var participant = await _groupParticipantRepository
                .GetByGroupAndUserAsync(groupId, userId);

            if (participant == null)
            {
                throw new AppException(
                    ErrorCodes.GroupMemberNotFound,
                    StatusCodes.Status404NotFound);
            }

            return participant;
        }

        /// <summary>
        /// Get User or throw exception
        /// </summary>
        private async Task<User> GetUserOrThrowAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);

            if (user == null)
            {
                throw new AppException(
                    ErrorCodes.UserNotFound,
                    StatusCodes.Status404NotFound);
            }

            return user;
        }
    }
}
