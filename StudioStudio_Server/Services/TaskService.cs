using Microsoft.IdentityModel.Tokens;
using Org.BouncyCastle.Asn1.Ocsp;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    public class TaskService : ITaskService
    {
        private readonly ILogger<TaskService> _logger;
        private readonly IMessageService _messageService;
        private readonly ITaskRepository _taskRepository;
        private readonly IGroupParticipantRepository _participantRepository;
        private readonly IGroupTaskStatusRepository _groupTaskStatusRepository;
        private readonly ITaskAssignmentRepository _taskAssignmentRepository;
        private readonly IUserRepository _userRepository;
        private readonly ITaskHistoryRepository _taskHistoryRepository;

        public TaskService(
            ITaskRepository taskRepository,
            ILogger<TaskService> logger,
            IMessageService message,
            IGroupParticipantRepository participantRepository,
            IGroupTaskStatusRepository groupTaskStatusRepository,
            ITaskAssignmentRepository taskAssignmentRepository,
            IUserRepository userRepository,
            ITaskHistoryRepository taskHistoryRepository)
        {
            _taskRepository = taskRepository;
            _logger = logger;
            _messageService = message;
            _participantRepository = participantRepository;
            _groupTaskStatusRepository = groupTaskStatusRepository;
            _taskAssignmentRepository = taskAssignmentRepository;
            _userRepository = userRepository;
            _taskHistoryRepository = taskHistoryRepository;
        }

        public async Task<TaskItemResponse> AddGroupTaskAsync(Guid userId, TaskItemGroupRequest request)
        {
            var userRole = await _participantRepository.GetGroupRoleByUserIdAsync(userId, request.GroupId);
            if (userRole.Equals(GroupRole.Viewer) || userRole.Equals(GroupRole.Commenter))
            {
                throw new AppException(ErrorCodes.GroupCreateTaskDenied, StatusCodes.Status401Unauthorized);
            }

            if (!request.GroupStatusId.HasValue)
            {
                throw new AppException(ErrorCodes.GroupCreateTaskDeniedMissingStatus, StatusCodes.Status400BadRequest);
            }

            var groupStatus = await _groupTaskStatusRepository.GetDetailAsync(request.GroupId);

            var existingTask = await _taskRepository.GetAllTasksByStatusIdAsync(request.GroupStatusId.Value);
            int newTaskPosition = 0;
            if (existingTask.Any())
            {
                newTaskPosition = existingTask.Max(s => s.Position) + 1000;
            }
            else
            {
                newTaskPosition = 1000;
            }

            var now = DateTime.UtcNow;

            if (request.StartDate > request.DueDate)
            {
                request.StartDate = request.DueDate;
            }
            if (request.DueDate < now)
            {
                request.DueDate = now;
            }

            var taskItem = new TaskItem
            {
                TaskId = Guid.NewGuid(),
                GroupId = request.GroupId,
                OwnerId = userId,
                GroupStatusId = request.GroupStatusId,
                Title = request.TaskName,
                Position = newTaskPosition,
                Description = request.TaskDescription,
                StartDate = request.StartDate,
                DueDate = request.DueDate,
                Priority = request.TaskPriority,
                Severity = request.TaskSeverity,
                IsPendingDeleted = false,
                CreatedAt = now,
                UpdatedAt = now,
            };

            await _taskRepository.AddAsync(taskItem);

            var assigneeId = request.Assignees != Guid.Empty ? request.Assignees : userId;
            var assignee = await _userRepository.GetByIdAsync(assigneeId);
            var assigneeDetail = new UserDto
            {
                Id = assigneeId,
                FirstName = assignee!.FirstName,
                LastName = assignee.LastName
            };

            return new TaskItemResponse
            {
                TaskId = taskItem.TaskId,
                TaskTitle = taskItem.Title,
                TaskDescription = taskItem.Description,
                TaskPriority = taskItem.Priority,
                TaskSeverity = taskItem.Severity,
                Position = taskItem.Position,
                CreatedById = taskItem.OwnerId,
                CreatedAt = now,
                StartDate = taskItem.StartDate.Value,
                DueDate = taskItem.DueDate.Value,
                GroupStatus = new GroupTaskStatusDto
                {
                    GroupId = request.GroupId,
                    StatusName = groupStatus!.StatusName,
                    Position = groupStatus.Position
                },
                Assignee = assigneeDetail,
            };
        }

        public async Task SoftDeleteTaskAsync(Guid userId, Guid groupId, Guid taskId)
        {
            var userRole = await _participantRepository.GetGroupRoleByUserIdAsync(userId, groupId);
            if (!userRole.Equals(GroupRole.Owner) && !userRole.Equals(GroupRole.Moderator))
            {
                throw new AppException(ErrorCodes.GroupDeleteTaskDenined, StatusCodes.Status401Unauthorized);
            }

            await _taskRepository.SoftDeleteAsync(taskId);
            await _taskHistoryRepository.AddAsync(new TaskHistory
            {
                HistoryId = Guid.NewGuid(),
                TaskId = taskId,
                ChangedBy = userId,
                ChangedAt = DateTime.UtcNow,
                ChangedContent = "DELETE"
            });
        }

        public async Task RestoreGroupTaskAsync(Guid userId, Guid groupId, Guid taskId)
        {
            var userRole = await _participantRepository.GetGroupRoleByUserIdAsync(userId, groupId);
            if (!userRole.Equals(GroupRole.Owner) && !userRole.Equals(GroupRole.Moderator))
            {
                throw new AppException(ErrorCodes.GroupRestoreTaskDenined, StatusCodes.Status401Unauthorized);
            }

            var task = await _taskRepository.GetByIdAsync(taskId);

            if (task == null)
            {
                return;
            }

            var statusList = await _groupTaskStatusRepository.GetByGroupIdAsync(groupId);

            //case where group delete all status
            if (!statusList.Any())
            {
                throw new AppException(ErrorCodes.GroupRestoreTaskFailed, StatusCodes.Status400BadRequest);
            }

            //case where old task status have been deleted
            if (!task.GroupStatusId.HasValue)
            {
                var firstStatus = statusList.OrderBy(s => s.Position).First();
                task.GroupStatusId = firstStatus.StatusId;
                var existingTask = await _taskRepository.GetAllTasksByStatusIdAsync(firstStatus.StatusId);
                int newTaskPosition = 0;
                if (existingTask.Any())
                {
                    newTaskPosition = existingTask.Max(s => s.Position) + 1000;
                }
                else
                {
                    newTaskPosition = 1000;
                }
                task.Position = newTaskPosition;
            }

            task.UpdatedAt = DateTime.UtcNow;
            await _taskRepository.RestoreAsync(task);
        }
        public async Task<List<TaskDeleteResponse>> GetDeleteTaskListAsync(Guid userId, Guid groupId)
        {
            var existedUser = await _participantRepository.GetByGroupAndUserAsync(groupId, userId);
            if (existedUser == null)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status400BadRequest);
            }

            var taskList = await _taskRepository.GetSoftDeleteTaskByGroup(groupId);
            var taskListId = taskList.Select(t => t.TaskId).ToList();
            var taskHistory = await _taskHistoryRepository.GetListTaskHistoryByTaskIdsAsync(taskListId);

            var result = taskHistory.Select(t => new TaskDeleteResponse
            {
                TaskName = taskList.FirstOrDefault(task => task.TaskId == t.TaskId)?.Title ?? "Unknown Task",
                DeletedOn = t.ChangedAt,
                DeletedBy = t.ChangedBy
            }).ToList();

            return result;
        }
    }
}
