using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
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

        public TaskService(
            ITaskRepository taskRepository,
            ILogger<TaskService> logger,
            IMessageService message,
            IGroupParticipantRepository participantRepository,
            IGroupTaskStatusRepository groupTaskStatusRepository)
        {
            _taskRepository = taskRepository;
            _logger = logger;
            _messageService = message;
            _participantRepository = participantRepository;
            _groupTaskStatusRepository = groupTaskStatusRepository;
        }

        public async Task<TaskItemResponse> AddGroupTaskAsync(Guid userId, TaskItemGroupRequest request)
        {
            var userRole = await _participantRepository.GetGroupRoleByUserIdAsync(userId, request.GroupId);
            if (userRole.Equals(GroupRole.Viewer) || userRole.Equals(GroupRole.Commenter))
            {
                throw new AppException(ErrorCodes.GroupCreateTaskDenied, StatusCodes.Status401Unauthorized);
            }

            if (request.GroupStatusId.Value == null)
            {
                throw new AppException(ErrorCodes.GroupCreateTaskDeniedMissingStatus, StatusCodes.Status400BadRequest);
            }

            var groupStatus = await _groupTaskStatusRepository.GetDetailAsync(request.GroupId);
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
                    StatusName = groupStatus.StatusName,
                    Position = groupStatus.Position
                },
                Assignee = new List<UserDto>()
            };
        }
    }
}
