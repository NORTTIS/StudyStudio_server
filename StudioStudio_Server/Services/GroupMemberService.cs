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
    public class GroupMemberService(
        IGroupRepository groupRepository,
        IGroupParticipantRepository groupParticipantRepository,
        IUserRepository userRepository,
        ILogger<GroupMemberService> logger) : IGroupMemberService
    {
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
                    ErrorCodes.GroupCannotRemoveSelf);
            }

            var targetMember = await groupParticipantRepository
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
                    ErrorCodes.GroupCannotRemoveOwner);
            }

            if (currentUserParticipant.Role == GroupRole.Moderator &&
                targetMember.Role == GroupRole.Moderator)
            {
                throw new AppException(
                    ErrorCodes.GroupPermissionDenied,
                    StatusCodes.Status403Forbidden);
            }

            var removedUser = await GetUserOrThrowAsync(request.UserId);

            await groupParticipantRepository.RemoveAsync(targetMember);

            logger.LogInformation(
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
        /// Leave a group (self-remove)
        /// Validate:
        /// - User must be an approved member of the group
        /// - Owner cannot leave (must transfer ownership or delete group)
        /// </summary>
        public async Task<LeaveGroupResponse> LeaveGroupAsync(Guid userId, Guid groupId)
        {
            var group = await ValidateGroupExistsAsync(groupId);

            var participant = await groupParticipantRepository
                .GetByGroupAndUserAsync(groupId, userId);

            if (participant == null)
            {
                throw new AppException(
                    ErrorCodes.GroupNotFound,
                    StatusCodes.Status404NotFound);
            }

            if (participant.Role == GroupRole.Owner)
            {
                throw new AppException(
                    ErrorCodes.GroupCannotLeaveAsOwner,
                    StatusCodes.Status403Forbidden);
            }

            await groupParticipantRepository.RemoveAsync(participant);

            logger.LogInformation(
                "User {UserId} left group {GroupId}",
                userId, groupId);

            return new LeaveGroupResponse
            {
                GroupId = group.GroupId,
                GroupName = group.GroupName,
                LeftAt = DateTime.UtcNow
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
            if (!Enum.TryParse(request.Role, true, out GroupRole newRole))
            {
                throw new AppException(
                    ErrorCodes.InviteInvalidRole);
            }

            var group = await ValidateGroupExistsAsync(request.GroupId);

            if (group.IsArchived)
            {
                throw new AppException(
                    ErrorCodes.GroupIsArchived,
                    StatusCodes.Status403Forbidden);
            }

            await ValidateUserIsOwnerAsync(
                request.GroupId,
                currentUserId);

            if (request.UserId == currentUserId)
            {
                throw new AppException(
                    ErrorCodes.GroupCannotChangeOwnRole);
            }

            var targetMember = await groupParticipantRepository
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
                    ErrorCodes.GroupOnlyOneOwner);
            }

            if (newRole == GroupRole.Moderator)
            {
                int moderatorCount = await groupParticipantRepository
                    .GetRoleCountByGroupIdAsync(request.GroupId, GroupRole.Moderator);

                if (moderatorCount > 0 && targetMember.Role != GroupRole.Moderator)
                {
                    throw new AppException(
                        ErrorCodes.GroupOnlyOneModerator);
                }
            }

            targetMember.Role = newRole;
            await groupParticipantRepository.UpdateAsync(targetMember);

            var user = await GetUserOrThrowAsync(request.UserId);

            logger.LogInformation(
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
            var group = await groupRepository.GetByIdAsync(groupId);

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
            var participant = await groupParticipantRepository
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
            var participant = await groupParticipantRepository
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
        /// Get User or throw exception
        /// </summary>
        private async Task<User> GetUserOrThrowAsync(Guid userId)
        {
            var user = await userRepository.GetByIdAsync(userId);

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
