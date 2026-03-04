using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using System.Threading.Tasks;

namespace StudioStudio_Server.Services
{
    public class GroupTaskStatusService : IGroupTaskStatusService
    {
        private readonly IGroupTaskStatusRepository _groupTaskStatusRepository;
        private readonly IGroupParticipantRepository _participantRepository;
        public GroupTaskStatusService(
            IGroupTaskStatusRepository groupTaskStatusRepository,
            IGroupParticipantRepository participantRepository)
        {
            _groupTaskStatusRepository = groupTaskStatusRepository;
            _participantRepository = participantRepository;
        }
        public async Task<GroupTaskStatusResponse> CreateNewGroupTaskStatus(Guid userId, Guid groupId, GroupTaskStatusRequest request)
        {
            var userRole = await _participantRepository.GetGroupRoleByUserIdAsync(userId, groupId);
            if (userRole.Equals(GroupRole.Viewer) || userRole.Equals(GroupRole.Commenter))
            {
                throw new AppException(ErrorCodes.GroupCreateTaskStatusDenied, StatusCodes.Status401Unauthorized);
            }

            var existingStatuses = await _groupTaskStatusRepository.GetByGroupIdAsync(groupId);

            int newPosition;
            if (existingStatuses.Any())
            {
                newPosition = existingStatuses.Max(s => s.Position) + 1000;
            }
            else
            {
                newPosition = 1000;
            }

            var newStatus = new GroupTaskStatus
            {
                StatusId = Guid.NewGuid(),
                GroupId = groupId,
                StatusName = request.StatusName,
                Position = newPosition,
            };

            if (await _groupTaskStatusRepository.NameExistsInGroupAsync(newStatus))
            {
                throw new AppException(ErrorCodes.GroupTaskStatusNameExist, StatusCodes.Status400BadRequest);
            }

            await _groupTaskStatusRepository.AddAsync(newStatus);

            return new GroupTaskStatusResponse
            {
                GroupId = groupId,
                StatusName = request.StatusName,
                Position = newPosition,
            };
        }

        public async Task<GroupTaskStatusResponse> GetGroupTaskStatusDetail(Guid taskStatusId)
        {
            var taskStatus = await _groupTaskStatusRepository.GetDetailAsync(taskStatusId);
            return new GroupTaskStatusResponse
            {
                GroupId = taskStatus.GroupId,
                StatusName = taskStatus.StatusName,
                Position = taskStatus.Position,
            };
        }

        public async Task SoftDeleteGroupTaskStatus(Guid userId, Guid groupId, Guid taskStatusId)
        {
            var userRole = await _participantRepository.GetGroupRoleByUserIdAsync(userId, groupId);
            if (!userRole.Equals(GroupRole.Moderator) && !userRole.Equals(GroupRole.Owner))
            {
                throw new AppException(ErrorCodes.GroupDeleteTaskStatusDenied, StatusCodes.Status401Unauthorized);
            }
            var taskStatus = await _groupTaskStatusRepository.GetDetailAsync(taskStatusId);
            if (taskStatus != null)
            {
                await _groupTaskStatusRepository.SoftDeleteAsync(taskStatus);
            }
        }

        public async Task UpdateGroupTaskStatus(Guid userId, Guid groupId, Guid taskStatusId, GroupTaskStatusRequest request)
        {
            var userRole = await _participantRepository.GetGroupRoleByUserIdAsync(userId, groupId);
            if (!userRole.Equals(GroupRole.Moderator) && !userRole.Equals(GroupRole.Owner))
            {
                throw new AppException(ErrorCodes.GroupUpdatePermissionDenied, StatusCodes.Status401Unauthorized);
            }

            var taskStatus = await _groupTaskStatusRepository.GetDetailAsync(taskStatusId);
            if (taskStatus == null || taskStatus.GroupId != groupId)
            {
                throw new AppException(ErrorCodes.TaskNotFound, StatusCodes.Status404NotFound);
            }

            taskStatus.StatusName = request.StatusName;

            if (await _groupTaskStatusRepository.NameExistsInGroupAsync(taskStatus))
            {
                throw new AppException(ErrorCodes.GroupTaskStatusNameExist, StatusCodes.Status400BadRequest);
            }

            await _groupTaskStatusRepository.UpdateAsync(taskStatus);
        }

        public async Task ReorderGroupTaskStatus(Guid userId, Guid groupId, ReorderGroupTaskStatusRequest request)
        {
            var userRole = await _participantRepository.GetGroupRoleByUserIdAsync(userId, groupId);
            if (userRole.Equals(GroupRole.Viewer) || userRole.Equals(GroupRole.Commenter))
            {
                throw new AppException(ErrorCodes.GroupUpdatePermissionDenied, StatusCodes.Status401Unauthorized);
            }

            var status = await _groupTaskStatusRepository.GetDetailAsync(request.StatusId);
            if (status == null || status.GroupId != groupId)
            {
                throw new AppException(ErrorCodes.TaskNotFound, StatusCodes.Status404NotFound);
            }

            await _groupTaskStatusRepository.ReorderStatusAsync(
                request.StatusId,
                request.PrevStatusId,
                request.NextStatusId,
                groupId
            );
        }
    }
}
