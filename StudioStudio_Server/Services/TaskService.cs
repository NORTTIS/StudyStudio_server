using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using Org.BouncyCastle.Asn1.Ocsp;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using StudioStudio_Server.Utils;

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
        private readonly IPersonalTaskStatusRepository _personalTaskStatusRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TaskService(
            ITaskRepository taskRepository,
            ILogger<TaskService> logger,
            IMessageService message,
            IGroupParticipantRepository participantRepository,
            IGroupTaskStatusRepository groupTaskStatusRepository,
            ITaskAssignmentRepository taskAssignmentRepository,
            IUserRepository userRepository,
            ITaskHistoryRepository taskHistoryRepository,
            IPersonalTaskStatusRepository personalTaskStatusRepository,
            IHttpContextAccessor httpContextAccessor)
        {
            _taskRepository = taskRepository;
            _logger = logger;
            _messageService = message;
            _participantRepository = participantRepository;
            _groupTaskStatusRepository = groupTaskStatusRepository;
            _taskAssignmentRepository = taskAssignmentRepository;
            _userRepository = userRepository;
            _taskHistoryRepository = taskHistoryRepository;
            _personalTaskStatusRepository = personalTaskStatusRepository;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Validate TaskPriority enum value
        /// </summary>
        private static void ValidateTaskPriority(TaskPriority priority)
        {
            if (!Enum.IsDefined(typeof(TaskPriority), priority))
            {
                throw new AppException(ErrorCodes.TaskInvalidPriority, StatusCodes.Status400BadRequest);
            }
        }

        /// <summary>
        /// Validate TaskSeverity enum value
        /// </summary>
        private static void ValidateTaskSeverity(TaskSeverity severity)
        {
            if (!Enum.IsDefined(typeof(TaskSeverity), severity))
            {
                throw new AppException(ErrorCodes.TaskInvalidSeverity, StatusCodes.Status400BadRequest);
            }
        }

        public async Task<TaskItemResponse> AddGroupTaskAsync(Guid userId, TaskItemGroupRequest request)
        {
            // Validate enums
            ValidateTaskPriority(request.TaskPriority);
            ValidateTaskSeverity(request.TaskSeverity);

            var userRole = await _participantRepository.GetGroupRoleByUserIdAsync(userId, request.GroupId);
            if (userRole.Equals(GroupRole.Viewer) || userRole.Equals(GroupRole.Commenter))
            {
                throw new AppException(ErrorCodes.GroupCreateTaskDenied, StatusCodes.Status403Forbidden);
            }

            if (!request.GroupStatusId.HasValue)
            {
                throw new AppException(ErrorCodes.GroupCreateTaskDeniedMissingStatus, StatusCodes.Status400BadRequest);
            }

            var groupStatus = await _groupTaskStatusRepository.GetDetailAsync(request.GroupStatusId.Value);

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

            // Convert dates to UTC if provided
            DateTime? startDateUtc = request.StartDate.HasValue
                ? DateTime.SpecifyKind(request.StartDate.Value, DateTimeKind.Utc)
                : null;

            DateTime? dueDateUtc = request.DueDate.HasValue
                ? DateTime.SpecifyKind(request.DueDate.Value, DateTimeKind.Utc)
                : null;

            // Validate dates
            if (startDateUtc.HasValue && dueDateUtc.HasValue)
            {
                if (startDateUtc.Value > dueDateUtc.Value)
                {
                    throw new AppException(ErrorCodes.TaskDateTimeError, StatusCodes.Status400BadRequest);
                }
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
                StartDate = startDateUtc,
                DueDate = dueDateUtc,
                Priority = request.TaskPriority,
                Severity = request.TaskSeverity,
                Progress = 0,
                IsPendingDeleted = false,
                CreatedAt = now,
                UpdatedAt = now,
            };

            await _taskRepository.AddAsync(taskItem);

            var assigneeId = request.Assignees;
            var assigneeDetail = new UserDto();

            if (assigneeId.HasValue && assigneeId.Value != Guid.Empty)
            {
                var assignee = await _userRepository.GetByIdAsync(assigneeId.Value);

                if (assignee == null)
                    throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);

                assigneeDetail = new UserDto
                {
                    Id = assignee.UserId,
                    FirstName = assignee.FirstName,
                    LastName = assignee.LastName
                };

                await _taskAssignmentRepository.AddAsync(new TaskAssignment
                {
                    AssignmentId = Guid.NewGuid(),
                    AssignedTo = assigneeDetail.Id,
                    AssignedBy = userId,
                    AssignedAt = DateTime.UtcNow,
                    TaskId = taskItem.TaskId
                });
            }
            return new TaskItemResponse
            {
                TaskId = taskItem.TaskId,
                TaskTitle = taskItem.Title,
                TaskDescription = taskItem.Description,
                TaskPriority = taskItem.Priority,
                TaskSeverity = taskItem.Severity,
                Position = taskItem.Position,
                Progress = taskItem.Progress,
                CreatedById = taskItem.OwnerId,
                CreatedAt = now,
                StartDate = taskItem.StartDate,
                DueDate = taskItem.DueDate,
                GroupStatus = new GroupTaskStatusDto
                {
                    GroupId = request.GroupId,
                    StatusName = groupStatus!.StatusName,
                    Position = groupStatus.Position
                },
                Assignee = assigneeDetail,
            };
        }

        public async Task<TaskItemResponse> UpdateGroupTaskAsync(Guid userId, Guid groupId, Guid taskId, UpdateTaskRequest request)
        {
            var userRole = await _participantRepository.GetGroupRoleByUserIdAsync(userId, groupId);
            if (userRole.Equals(GroupRole.Viewer) || userRole.Equals(GroupRole.Commenter))
            {
                throw new AppException(ErrorCodes.GroupUpdatePermissionDenied, StatusCodes.Status403Forbidden);
            }

            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null)
            {
                throw new AppException(ErrorCodes.TaskNotFound, StatusCodes.Status404NotFound);
            }

            if (task.GroupId != groupId)
            {
                throw new AppException(ErrorCodes.TaskNotFound, StatusCodes.Status404NotFound);
            }

            // Validate GroupStatusId if provided
            if (request.GroupStatusId.HasValue)
            {
                var statusExists = await _groupTaskStatusRepository.GetDetailAsync(request.GroupStatusId.Value);
                if (statusExists == null || statusExists.GroupId != groupId)
                {
                    throw new AppException(ErrorCodes.GroupStatusNotFound, StatusCodes.Status404NotFound);
                }
            }

            // Convert dates to UTC if provided
            DateTime? startDateUtc = request.StartDate.HasValue
                ? DateTime.SpecifyKind(request.StartDate.Value, DateTimeKind.Utc)
                : null;

            DateTime? dueDateUtc = request.DueDate.HasValue
                ? DateTime.SpecifyKind(request.DueDate.Value, DateTimeKind.Utc)
                : null;

            // Validate dates
            if (startDateUtc.HasValue && dueDateUtc.HasValue)
            {
                if (startDateUtc.Value > dueDateUtc.Value)
                {
                    throw new AppException(ErrorCodes.TaskDateTimeError, StatusCodes.Status400BadRequest);
                }
            }

            // Update basic fields
            if (!string.IsNullOrWhiteSpace(request.TaskName))
            {
                task.Title = request.TaskName;
            }

            if (request.TaskDescription != null)
            {
                task.Description = request.TaskDescription;
            }

            if (request.TaskPriority.HasValue)
            {
                ValidateTaskPriority(request.TaskPriority.Value);
                task.Priority = request.TaskPriority.Value;
            }

            if (request.TaskSeverity.HasValue)
            {
                ValidateTaskSeverity(request.TaskSeverity.Value);
                task.Severity = request.TaskSeverity.Value;
            }

            if (startDateUtc.HasValue)
            {
                task.StartDate = startDateUtc.Value;
            }

            if (dueDateUtc.HasValue)
            {
                task.DueDate = dueDateUtc.Value;
            }

            if (request.Progress.HasValue)
            {
                task.Progress = request.Progress.Value;
            }

            // Update GroupStatusId if provided
            if (request.GroupStatusId.HasValue)
            {
                task.GroupStatusId = request.GroupStatusId.Value;
            }

            await _taskRepository.UpdateAsync(task);

            // Handle assignee update if provided
            if (request.AssigneeId.HasValue)
            {
                // Get existing assignments
                var existingAssignments = await _taskAssignmentRepository.GetAssigneesByTaskId(taskId);

                // If AssigneeId is Guid.Empty, remove all assignments
                if (request.AssigneeId.Value == Guid.Empty)
                {
                    if (existingAssignments.Any())
                    {
                        await _taskAssignmentRepository.RemoveAsync(existingAssignments);
                    }
                }
                else
                {
                    // Validate new assignee exists
                    var newAssignee = await _userRepository.GetByIdAsync(request.AssigneeId.Value);
                    if (newAssignee == null)
                    {
                        throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
                    }

                    // Check if assignee is already assigned
                    var alreadyAssigned = existingAssignments.Any(a => a.AssignedTo == request.AssigneeId.Value);

                    if (!alreadyAssigned)
                    {
                        // Remove existing assignments
                        if (existingAssignments.Any())
                        {
                            await _taskAssignmentRepository.RemoveAsync(existingAssignments);
                        }

                        // Add new assignment
                        await _taskAssignmentRepository.AddAsync(new TaskAssignment
                        {
                            AssignmentId = Guid.NewGuid(),
                            AssignedTo = request.AssigneeId.Value,
                            AssignedBy = userId,
                            AssignedAt = DateTime.UtcNow,
                            TaskId = taskId
                        });
                    }
                }
            }

            // Prepare response
            var groupStatus = task.GroupStatusId.HasValue
                ? await _groupTaskStatusRepository.GetDetailAsync(task.GroupStatusId.Value)
                : null;

            var assignmentList = await _taskAssignmentRepository.GetAssigneesByTaskId(taskId);
            var assignment = assignmentList.FirstOrDefault();
            var assigneeDetail = new UserDto();

            if (assignment != null)
            {
                var assignee = await _userRepository.GetByIdAsync(assignment.AssignedTo);
                if (assignee != null)
                {
                    assigneeDetail = new UserDto
                    {
                        Id = assignee.UserId,
                        FirstName = assignee.FirstName,
                        LastName = assignee.LastName
                    };
                }
            }

            return new TaskItemResponse
            {
                TaskId = task.TaskId,
                TaskTitle = task.Title,
                TaskDescription = task.Description ?? string.Empty,
                TaskPriority = task.Priority,
                TaskSeverity = task.Severity,
                Position = task.Position,
                Progress = task.Progress,
                CreatedById = task.OwnerId,
                CreatedAt = task.CreatedAt,
                StartDate = task.StartDate,
                DueDate = task.DueDate,
                GroupStatus = groupStatus != null ? new GroupTaskStatusDto
                {
                    GroupId = groupId,
                    StatusName = groupStatus.StatusName,
                    Position = groupStatus.Position
                } : null,
                Assignee = assigneeDetail,
            };
        }

        public async Task SoftDeleteTaskAsync(Guid userId, Guid groupId, Guid taskId)
        {
            var userRole = await _participantRepository.GetGroupRoleByUserIdAsync(userId, groupId);
            if (!userRole.Equals(GroupRole.Owner) && !userRole.Equals(GroupRole.Moderator))
            {
                throw new AppException(ErrorCodes.GroupDeleteTaskDenined, StatusCodes.Status403Forbidden);
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

        public async Task DeletePersonalTaskAsync(Guid userId, Guid taskId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status403Forbidden);
            }

            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null || task.OwnerId != userId || task.GroupId.HasValue)
            {
                throw new AppException(ErrorCodes.TaskNotFound, StatusCodes.Status404NotFound);
            }

            await _taskRepository.PermanentDeleteAsync(taskId);
        }

        public async Task RestoreGroupTaskAsync(Guid userId, Guid groupId, Guid taskId)
        {
            var userRole = await _participantRepository.GetGroupRoleByUserIdAsync(userId, groupId);
            if (!userRole.Equals(GroupRole.Owner) && !userRole.Equals(GroupRole.Moderator))
            {
                throw new AppException(ErrorCodes.GroupRestoreTaskDenined, StatusCodes.Status401Unauthorized);
            }

            var task = await _taskRepository.GetDeletedByIdAsync(taskId);

            if (task == null)
            {
                throw new AppException(ErrorCodes.TaskNotFound, StatusCodes.Status404NotFound);
            }

            var statusList = await _groupTaskStatusRepository.GetByGroupIdAsync(groupId);

            //case where group delete all status
            if (!statusList.Any())
            {
                throw new AppException(ErrorCodes.GroupRestoreTaskFailed, StatusCodes.Status403Forbidden);
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

            //normal case
            if (task.GroupStatusId.HasValue)
            {
                var statusExistTask = await _taskRepository.GetAllTasksByStatusIdAsync(task.GroupStatusId.Value);
                int taskNewPosition = 0;
                if (statusExistTask.Any())
                {
                    taskNewPosition = statusExistTask.Max(s => s.Position) + 1000;
                }
                else
                {
                    taskNewPosition = 1000;
                }
                task.Position = taskNewPosition;
            }

            task.UpdatedAt = DateTime.UtcNow;
            await _taskRepository.RestoreAsync(task);
        }
        public async Task<List<TaskDeleteResponse>> GetDeleteTaskListAsync(Guid userId, Guid groupId)
        {
            var existedUser = await _participantRepository.GetByGroupAndUserAsync(groupId, userId);
            if (existedUser == null)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            var taskList = await _taskRepository.GetSoftDeleteTaskByGroup(groupId);
            var taskListId = taskList.Select(t => t.TaskId).ToList();
            var taskHistory = await _taskHistoryRepository.GetListTaskHistoryByTaskIdsAsync(taskListId);

            var result = taskHistory.Select(t => new TaskDeleteResponse
            {
                DeleteTaskId = t.TaskId,
                TaskName = taskList.FirstOrDefault(task => task.TaskId == t.TaskId)?.Title ?? "Unknown Task",
                DeletedOn = t.ChangedAt,
                DeletedBy = t.ChangedBy
            }).ToList();

            return result;
        }

        public async Task ReorderTaskAsync(Guid userId, Guid groupId, ReorderTaskRequest request)
        {
            var userRole = await _participantRepository.GetGroupRoleByUserIdAsync(userId, groupId);
            if (userRole.Equals(GroupRole.Viewer) || userRole.Equals(GroupRole.Commenter))
            {
                throw new AppException(ErrorCodes.GroupUpdatePermissionDenied, StatusCodes.Status403Forbidden);
            }
            var task = await _taskRepository.GetByIdAsync(request.TaskId);
            if (task == null)
            {
                throw new AppException(ErrorCodes.TaskNotFound, StatusCodes.Status404NotFound);
            }

            await _taskRepository.ReorderTaskAsync(
            request.TaskId,
            request.TargetStatusId,
            request.PrevTaskId,
            request.NextTaskId);
        }

        public async Task<TaskItemResponse> AddPersonalTaskAsync(Guid userId, TaskItemPersonalRequest request)
        {
            // Validate enums
            ValidateTaskPriority(request.TaskPriority);
            ValidateTaskSeverity(request.TaskSeverity);

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
            }

            if (!request.PersonalStatusId.HasValue)
            {
                throw new AppException(ErrorCodes.PersonalCreateTaskDeniedMissingStatus, StatusCodes.Status400BadRequest);
            }

            var personalTaskStatus = await _personalTaskStatusRepository.GetDetailAsync(request.PersonalStatusId.Value);
            if (personalTaskStatus == null || personalTaskStatus.UserId != userId)
            {
                throw new AppException(ErrorCodes.StatusNotFound, StatusCodes.Status404NotFound);
            }

            var existingTask = await _taskRepository.GetAllPersonalTasksByStatusIdAsync(request.PersonalStatusId.Value);
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

            // Convert dates to UTC if provided
            DateTime? startDateUtc = request.StartDate.HasValue
                ? DateTime.SpecifyKind(request.StartDate.Value, DateTimeKind.Utc)
                : null;

            DateTime? dueDateUtc = request.DueDate.HasValue
                ? DateTime.SpecifyKind(request.DueDate.Value, DateTimeKind.Utc)
                : null;

            // Validate dates
            if (startDateUtc.HasValue && dueDateUtc.HasValue)
            {
                if (startDateUtc.Value > dueDateUtc.Value)
                {
                    throw new AppException(ErrorCodes.TaskDateTimeError, StatusCodes.Status400BadRequest);
                }
            }

            var taskItem = new TaskItem
            {
                TaskId = Guid.NewGuid(),
                OwnerId = userId,
                GroupId = null,
                GroupStatusId = null,
                PersonalStatusId = request.PersonalStatusId,
                Title = request.TaskName,
                Position = newTaskPosition,
                Description = request.TaskDescription,
                StartDate = startDateUtc,
                DueDate = dueDateUtc,
                Priority = request.TaskPriority,
                Severity = request.TaskSeverity,
                Progress = 0,
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
                Progress = taskItem.Progress,
                CreatedById = taskItem.OwnerId,
                CreatedAt = now,
                StartDate = taskItem.StartDate,
                DueDate = taskItem.DueDate,
                PersonalStatus = new PersonalTaskStatusDto
                {
                    UserId = userId,
                    StatusName = personalTaskStatus.StatusName,
                    Position = personalTaskStatus.Position
                },
                Assignee = new UserDto
                {
                    Id = user.UserId,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    AvatarUrl = AvatarUrlHelper.BuildAbsoluteAvatarUrl(user.AvatarUrl, _httpContextAccessor.HttpContext)
                }
            };
        }
        public async Task<TaskItemResponse> UpdatePersonalTaskAsync(Guid userId, Guid taskId, UpdatePersonalTaskRequest request)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
            }

            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null || task.OwnerId != userId || task.GroupId.HasValue)
            {
                throw new AppException(ErrorCodes.TaskNotFound, StatusCodes.Status404NotFound);
            }

            // Validate PersonalStatusId if provided
            if (request.PersonalStatusId.HasValue)
            {
                var statusExists = await _personalTaskStatusRepository.GetDetailAsync(request.PersonalStatusId.Value);
                if (statusExists == null || statusExists.UserId != userId)
                {
                    throw new AppException(ErrorCodes.StatusNotFound, StatusCodes.Status404NotFound);
                }
            }

            // Convert dates to UTC if provided
            DateTime? startDateUtc = request.StartDate.HasValue
                ? DateTime.SpecifyKind(request.StartDate.Value, DateTimeKind.Utc)
                : null;

            DateTime? dueDateUtc = request.DueDate.HasValue
                ? DateTime.SpecifyKind(request.DueDate.Value, DateTimeKind.Utc)
                : null;

            // Validate dates
            if (startDateUtc.HasValue && dueDateUtc.HasValue)
            {
                if (startDateUtc.Value > dueDateUtc.Value)
                {
                    throw new AppException(ErrorCodes.TaskDateTimeError, StatusCodes.Status400BadRequest);
                }
            }

            // Update basic fields
            if (!string.IsNullOrWhiteSpace(request.TaskName))
            {
                task.Title = request.TaskName;
            }

            if (request.TaskDescription != null)
            {
                task.Description = request.TaskDescription;
            }

            if (request.TaskPriority.HasValue)
            {
                ValidateTaskPriority(request.TaskPriority.Value);
                task.Priority = request.TaskPriority.Value;
            }

            if (request.TaskSeverity.HasValue)
            {
                ValidateTaskSeverity(request.TaskSeverity.Value);
                task.Severity = request.TaskSeverity.Value;
            }

            if (startDateUtc.HasValue)
            {
                task.StartDate = startDateUtc.Value;
            }

            if (dueDateUtc.HasValue)
            {
                task.DueDate = dueDateUtc.Value;
            }

            if (request.Progress.HasValue)
            {
                task.Progress = request.Progress.Value;
            }

            // Update personal if provided
            if (request.PersonalStatusId.HasValue)
            {
                task.PersonalStatusId = request.PersonalStatusId.Value;
            }

            await _taskRepository.UpdateAsync(task);

            // Prepare response
            var personalStatus = task.PersonalStatusId.HasValue
                ? await _personalTaskStatusRepository.GetDetailAsync(task.PersonalStatusId.Value)
                : null;

            return new TaskItemResponse
            {
                TaskId = task.TaskId,
                TaskTitle = task.Title,
                TaskDescription = task.Description ?? string.Empty,
                TaskPriority = task.Priority,
                TaskSeverity = task.Severity,
                Position = task.Position,
                Progress = task.Progress,
                CreatedById = task.OwnerId,
                CreatedAt = task.CreatedAt,
                StartDate = task.StartDate,
                DueDate = task.DueDate,
                PersonalStatus = personalStatus != null ? new PersonalTaskStatusDto
                {
                    UserId = personalStatus.UserId,
                    StatusName = personalStatus.StatusName,
                    Position = personalStatus.Position
                } : null,
                Assignee = new UserDto
                {
                    Id = user.UserId,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    AvatarUrl = AvatarUrlHelper.BuildAbsoluteAvatarUrl(user.AvatarUrl, _httpContextAccessor.HttpContext)
                }
            };
        }
        public async Task ReorderPersonalTaskAsync(Guid userId, ReorderTaskRequest request)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
            }

            var task = await _taskRepository.GetByIdAsync(request.TaskId);
            if (task == null || task.OwnerId != userId || task.GroupId.HasValue)
            {
                throw new AppException(ErrorCodes.TaskNotFound, StatusCodes.Status404NotFound);
            }

            var targetStatus = await _personalTaskStatusRepository.GetDetailAsync(request.TargetStatusId);
            if (targetStatus == null || targetStatus.UserId != userId)
            {
                throw new AppException(ErrorCodes.StatusNotFound, StatusCodes.Status404NotFound);
            }

            if (request.PrevTaskId.HasValue)
            {
                var prevTask = await _taskRepository.GetByIdAsync(request.PrevTaskId.Value);
                if (prevTask == null || prevTask.OwnerId != userId || prevTask.GroupId.HasValue || prevTask.PersonalStatusId != request.TargetStatusId)
                {
                    throw new AppException(ErrorCodes.TaskNotFound, StatusCodes.Status404NotFound);
                }
            }

            if (request.NextTaskId.HasValue)
            {
                var nextTask = await _taskRepository.GetByIdAsync(request.NextTaskId.Value);
                if (nextTask == null || nextTask.OwnerId != userId || nextTask.GroupId.HasValue || nextTask.PersonalStatusId != request.TargetStatusId)
                {
                    throw new AppException(ErrorCodes.TaskNotFound, StatusCodes.Status404NotFound);
                }
            }

            await _taskRepository.ReorderPersonalTaskAsync(
                request.TaskId,
                request.TargetStatusId,
                request.PrevTaskId,
                request.NextTaskId);
        }

        public async Task PermanentDeleteGroupTaskAsync(Guid userId, Guid groupId, Guid taskId)
        {
            var userRole = await _participantRepository.GetGroupRoleByUserIdAsync(userId, groupId);
            if (!userRole.Equals(GroupRole.Owner) && !userRole.Equals(GroupRole.Moderator))
            {
                throw new AppException(ErrorCodes.GroupDeleteTaskDenined, StatusCodes.Status403Forbidden);
            }

            var task = await _taskRepository.GetDeletedByIdAsync(taskId);
            if (task == null || task.GroupId != groupId)
            {
                throw new AppException(ErrorCodes.TaskNotFound, StatusCodes.Status404NotFound);
            }

            if (!task.IsPendingDeleted)
            {
                throw new AppException(ErrorCodes.TaskNotPendingDeleted, StatusCodes.Status400BadRequest);
            }

            await _taskRepository.PermanentDeleteAsync(taskId);
        }
    }
}
