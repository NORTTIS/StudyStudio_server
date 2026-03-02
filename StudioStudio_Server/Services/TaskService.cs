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

        public TaskService(
            ITaskRepository taskRepository,
            ILogger<TaskService> logger,
            IMessageService message,
            IGroupParticipantRepository participantRepository)
        {
            _taskRepository = taskRepository;
            _logger = logger;
            _messageService = message;
            _participantRepository = participantRepository;
        }

        public Task<TaskItemResponse> AddGroupTaskAsync(TaskItemGroupRequest request)
        {
            var userRole = _participantRepository.GetGroupRoleByUserIdAsync(request.CreatedById, request.GroupId);
            if (userRole.Equals(GroupRole.Viewer) || userRole.Equals(GroupRole.Commenter))
            {
                throw new AppException(ErrorCodes.GroupCreateTaskDenied, StatusCodes.Status401Unauthorized);
            }

            var now = DateTime.UtcNow;
            var taskItem = new TaskItem
            {
                TaskId = Guid.NewGuid(),
                GroupId = request.GroupId,
                OwnerId = request.CreatedById,
                GroupStatusId = request.GroupStatusId,
                Title = request.TaskName,
                Description = request.TaskDescription,
                DueDate = request.DueDate,
                Priority = request.TaskPriority,
                Severity = request.TaskSeverity,
                IsPendingDeleted = false
            };



            return null;
        }
    }
}
