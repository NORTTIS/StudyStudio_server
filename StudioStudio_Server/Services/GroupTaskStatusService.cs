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

            var newStatus = new GroupTaskStatus
            {
                StatusId = Guid.NewGuid(),
                GroupId = groupId,
                StatusName = request.StatusName,
                Position = request.Position,
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
                Position = request.Position,
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

        public async Task UpdateAllTaskStatusPosition(Guid userId, Guid groupId, List<GroupTaskStatusPositionRequest> requestList)
        {
            var userRole = await _participantRepository.GetGroupRoleByUserIdAsync(userId, groupId);
            if (userRole.Equals(GroupRole.Viewer) || userRole.Equals(GroupRole.Commenter))
            {
                throw new AppException(ErrorCodes.GroupUpdatePermissionDenied, StatusCodes.Status401Unauthorized);
            }

            var statusIds = requestList.Select(x => x.StatusId).ToList();

            var statuses = await _groupTaskStatusRepository.GetByIdsAndGroupIdAsync(statusIds, groupId);

            var positionMap = requestList.ToDictionary(x => x.StatusId, x => x.Position);
            foreach (var item in statuses)
            {
                item.Position = positionMap[item.StatusId];
            }

            await _groupTaskStatusRepository.SaveChangesAsync();
        }

        public async Task UpdateGroupTaskStatus(Guid userId, Guid groupId, Guid taskStatusId, GroupTaskStatusRequest request)
        {
            var userRole = await _participantRepository.GetGroupRoleByUserIdAsync(userId, groupId);
            if (!userRole.Equals(GroupRole.Moderator) && !userRole.Equals(GroupRole.Owner))
            {
                throw new AppException(ErrorCodes.GroupUpdatePermissionDenied, StatusCodes.Status401Unauthorized);
            }
            var taskStatus = await _groupTaskStatusRepository.GetDetailAsync(taskStatusId);

            if (taskStatus != null)
            {
                if (await _groupTaskStatusRepository.NameExistsInGroupAsync(taskStatus))
                {
                    throw new AppException(ErrorCodes.GroupTaskStatusNameExist, StatusCodes.Status400BadRequest);
                }
                taskStatus.StatusName = request.StatusName;
                await _groupTaskStatusRepository.UpdateAsync(taskStatus);
            }
        }
    }
}
