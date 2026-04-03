using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using System.Threading.Tasks;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service handling business logic for Group Task Status (Kanban columns)
    /// Manages CRUD operations for task status columns in groups
    /// Permission: Viewer and Commenter can only view, Member/Moderator/Owner can create/update/delete
    /// </summary>
    public class GroupTaskStatusService : IGroupTaskStatusService
    {
        private readonly IGroupTaskStatusRepository _groupTaskStatusRepository;
        private readonly IGroupParticipantRepository _participantRepository;
        private readonly ITaskRepository _taskRepository;
        
        public GroupTaskStatusService(
            IGroupTaskStatusRepository groupTaskStatusRepository,
            IGroupParticipantRepository participantRepository,
            ITaskRepository taskRepository)
        {
            _groupTaskStatusRepository = groupTaskStatusRepository;
            _participantRepository = participantRepository;
            _taskRepository = taskRepository;
        }
        
        /// <summary>
        /// Create new task status column for group
        /// Validate:
        /// - User must be Member, Moderator, or Owner (not Viewer/Commenter)
        /// - Status name must be unique in group
        /// Position: Auto-calculated as max(existing positions) + 1000
        /// </summary>
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
                throw new AppException(ErrorCodes.StatusNameExist, StatusCodes.Status400BadRequest);
            }

            await _groupTaskStatusRepository.AddAsync(newStatus);

            return new GroupTaskStatusResponse
            {
                GroupId = groupId,
                StatusName = request.StatusName,
                Position = newPosition,
            };
        }

        /// <summary>
        /// Get task status details by ID
        /// Returns: GroupTaskStatusResponse with GroupId, StatusName, Position
        /// </summary>
        public async Task<GroupTaskStatusResponse> GetGroupTaskStatusDetail(Guid taskStatusId)
        {
            var taskStatus = await _groupTaskStatusRepository.GetDetailAsync(taskStatusId)
                ?? throw new InvalidOperationException($"Task status {taskStatusId} not found");
            return new GroupTaskStatusResponse
            {
                GroupId = taskStatus.GroupId,
                StatusName = taskStatus.StatusName,
                Position = taskStatus.Position,
            };
        }

        /// <summary>
        /// Delete task status column from group
        /// Validate:
        /// - User must be Moderator or Owner
        /// - Status must belong to the group
        /// - Status must not contain any tasks (empty column only)
        /// </summary>
        public async Task DeleteGroupTaskStatus(Guid userId, Guid groupId, Guid taskStatusId)
        {
            var userRole = await _participantRepository.GetGroupRoleByUserIdAsync(userId, groupId);
            if (!userRole.Equals(GroupRole.Moderator) && !userRole.Equals(GroupRole.Owner))
            {
                throw new AppException(ErrorCodes.GroupDeleteTaskStatusDenied, StatusCodes.Status401Unauthorized);
            }
            var taskStatus = await _groupTaskStatusRepository.GetDetailAsync(taskStatusId);
            if (taskStatus == null || taskStatus.GroupId != groupId)
            {
                throw new AppException(ErrorCodes.GroupStatusNotFound, StatusCodes.Status404NotFound);
            }
            var taskList = await _taskRepository.GetAllTasksByStatusIdAsync(taskStatusId);
            if (taskList.Any())
            {
                throw new AppException(ErrorCodes.GroupDeleteTaskStatusFailed, StatusCodes.Status400BadRequest);
            }
            await _groupTaskStatusRepository.DeleteAsync(taskStatus);
        }

        /// <summary>
        /// Update task status name
        /// Validate:
        /// - User must be Moderator or Owner
        /// - Status must belong to the group
        /// - New status name must be unique in group
        /// </summary>
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
                throw new AppException(ErrorCodes.StatusNotFound, StatusCodes.Status404NotFound);
            }

            taskStatus.StatusName = request.StatusName;

            if (await _groupTaskStatusRepository.NameExistsInGroupAsync(taskStatus))
            {
                throw new AppException(ErrorCodes.StatusNameExist, StatusCodes.Status400BadRequest);
            }

            await _groupTaskStatusRepository.UpdateAsync(taskStatus);
        }

        /// <summary>
        /// Reorder task status column position (drag and drop)
        /// Validate:
        /// - User must be Member, Moderator, or Owner (not Viewer/Commenter)
        /// - Status must belong to the group
        /// Uses midpoint ranking algorithm with automatic rebalancing
        /// </summary>
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
                throw new AppException(ErrorCodes.StatusNotFound, StatusCodes.Status404NotFound);
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
