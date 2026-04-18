using Microsoft.IdentityModel.Tokens;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using StudioStudio_Server.Services.TaskNotificationQueue;
using StudioStudio_Server.Utils;

namespace StudioStudio_Server.Services
{
    public class TaskService(
        ITaskRepository taskRepository,
        ILogger<TaskService> logger,
        IMessageService message,
        IGroupRepository groupRepository,
        IGroupParticipantRepository participantRepository,
        IGroupTaskStatusRepository groupTaskStatusRepository,
        ITaskAssignmentRepository taskAssignmentRepository,
        IUserRepository userRepository,
        IPersonalTaskStatusRepository personalTaskStatusRepository,
        IHttpContextAccessor httpContextAccessor,
        IActivityLogService activityLogService,
        INotificationService notificationService,
        ITaskUpdateNotificationQueue taskUpdateNotificationQueue) : ITaskService
    {
        /// <summary>
        /// Validate TaskPriority enum value
        /// </summary>
        private static void ValidateTaskPriority(TaskPriority priority)
        {
            if (!Enum.IsDefined(typeof(TaskPriority), priority))
            {
                throw new AppException(ErrorCodes.TaskInvalidPriority);
            }
        }

        /// <summary>
        /// Validate TaskSeverity enum value
        /// </summary>
        private static void ValidateTaskSeverity(TaskSeverity severity)
        {
            if (!Enum.IsDefined(typeof(TaskSeverity), severity))
            {
                throw new AppException(ErrorCodes.TaskInvalidSeverity);
            }
        }

        /// <summary>
        /// Validate EstimatedHours or ActualHours does not exceed task duration
        /// </summary>
        private void ValidateHours(decimal? hours, DateTime? startDate, DateTime? dueDate, string fieldName)
        {
            if (hours == null || hours <= 0) return;

            if (startDate != null && dueDate != null)
            {
                var availableHours = (dueDate.Value - startDate.Value).TotalHours;
                if (availableHours > 0 && (double)hours > availableHours)
                {
                    throw new AppException(
                        $"'{fieldName}' không được lớn hơn khoảng thời gian từ StartDate đến DueDate ({availableHours:F1} giờ).");
                }
            }
        }

        public async Task<TaskItemResponse> AddGroupTaskAsync(Guid userId, TaskItemGroupRequest request)
        {
            // Validate enums
            ValidateTaskPriority(request.TaskPriority);
            ValidateTaskSeverity(request.TaskSeverity);

            var userRole = await participantRepository.GetGroupRoleByUserIdAsync(userId, request.GroupId);
            if (userRole.Equals(GroupRole.Viewer) || userRole.Equals(GroupRole.Commenter))
            {
                throw new AppException(ErrorCodes.GroupCreateTaskDenied, StatusCodes.Status403Forbidden);
            }

            // Check if group is archived (non-owner cannot interact)
            var group = await groupRepository.GetByIdAsync(request.GroupId);
            if (group != null && group.IsArchived && userRole != GroupRole.Owner)
            {
                throw new AppException(ErrorCodes.GroupIsArchived, StatusCodes.Status403Forbidden);
            }

            if (!request.GroupStatusId.HasValue)
            {
                throw new AppException(ErrorCodes.GroupCreateTaskDeniedMissingStatus);
            }

            var groupStatus = await groupTaskStatusRepository.GetDetailAsync(request.GroupStatusId.Value);

            var existingTask = await taskRepository.GetAllTasksByStatusIdAsync(request.GroupStatusId.Value);
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
                    throw new AppException(ErrorCodes.TaskDateTimeError);
                }
            }

            // Validate hours
            ValidateHours(request.EstimatedHours, startDateUtc, dueDateUtc, "EstimatedHours");
            ValidateHours(request.ActualHours, startDateUtc, dueDateUtc, "ActualHours");

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
                EstimatedHours = request.EstimatedHours,
                ActualHours = request.ActualHours,
                Progress = 0,
                IsPendingDeleted = false,
                CreatedAt = now,
                UpdatedAt = now,
            };

            await taskRepository.AddAsync(taskItem);

            // Log task creation activity with priority/severity for weighted contribution scoring
            await activityLogService.LogTaskCreateAsync(
                userId, taskItem.TaskId, taskItem.GroupId, null,
                (int)taskItem.Priority, (int)taskItem.Severity);

            var assigneeId = request.Assignees;
            var assigneeDetail = new UserDto();

            if (assigneeId.HasValue && assigneeId.Value != Guid.Empty)
            {
                var assignee = await userRepository.GetByIdAsync(assigneeId.Value);

                if (assignee == null)
                    throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);

                assigneeDetail = new UserDto
                {
                    Id = assignee.UserId,
                    FirstName = assignee.FirstName,
                    LastName = assignee.LastName
                };

                await taskAssignmentRepository.AddAsync(new TaskAssignment
                {
                    AssignmentId = Guid.NewGuid(),
                    AssignedTo = assigneeDetail.Id,
                    AssignedBy = userId,
                    AssignedAt = DateTime.UtcNow,
                    TaskId = taskItem.TaskId
                });

                // Log task assignment activity
                await activityLogService.LogTaskAssignAsync(userId, taskItem.TaskId, assigneeDetail.Id, taskItem.GroupId);

                // Load current user for notification
                var currentUser = await userRepository.GetByIdAsync(userId);
                if (currentUser != null)
                {
                    // Notify assignment
                    await notificationService.NotifyTaskAssignedAsync(
                        assignee,
                        currentUser,
                        taskItem.TaskId,
                        taskItem.Title,
                        taskItem.DueDate);
                }
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
                EstimatedHours = taskItem.EstimatedHours,
                ActualHours = taskItem.ActualHours,
            };
        }

        public async Task<TaskItemResponse> UpdateGroupTaskAsync(Guid userId, Guid groupId, Guid taskId, UpdateTaskRequest request)
        {
            var userRole = await participantRepository.GetGroupRoleByUserIdAsync(userId, groupId);
            if (userRole.Equals(GroupRole.Viewer) || userRole.Equals(GroupRole.Commenter))
            {
                throw new AppException(ErrorCodes.GroupUpdatePermissionDenied, StatusCodes.Status403Forbidden);
            }

            // Check if group is archived (non-owner cannot interact)
            var group = await groupRepository.GetByIdAsync(groupId);
            if (group != null && group.IsArchived && userRole != GroupRole.Owner)
            {
                throw new AppException(ErrorCodes.GroupIsArchived, StatusCodes.Status403Forbidden);
            }

            var task = await taskRepository.GetByIdAsync(taskId);
            if (task == null)
            {
                throw new AppException(ErrorCodes.TaskNotFound, StatusCodes.Status404NotFound);
            }

            if (task.GroupId != groupId)
            {
                throw new AppException(ErrorCodes.TaskNotFound, StatusCodes.Status404NotFound);
            }

            // ============================================================
            // PHASE 2 OPTIMIZATION: Preload all statuses in one call
            // ============================================================
            var statusIdsToLoad = new List<Guid>();
            if (request.GroupStatusId.HasValue)
                statusIdsToLoad.Add(request.GroupStatusId.Value);
            if (task.GroupStatusId.HasValue)
                statusIdsToLoad.Add(task.GroupStatusId.Value);
            var statusMap = new Dictionary<Guid, GroupTaskStatus>();
            if (statusIdsToLoad.Count > 0)
            {
                var statuses = await groupTaskStatusRepository.GetByIdsAndGroupIdAsync(
                    statusIdsToLoad.Distinct().ToList(), groupId);
                statusMap = statuses.ToDictionary(s => s.StatusId);
            }

            // Validate GroupStatusId if provided
            if (request.GroupStatusId.HasValue)
            {
                if (!statusMap.TryGetValue(request.GroupStatusId.Value, out var statusExists) || statusExists.GroupId != groupId)
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
                    throw new AppException(ErrorCodes.TaskDateTimeError);
                }
            }

            // Effective dates for hours validation: use request values or existing task values
            var effectiveStartDate = startDateUtc ?? task.StartDate;
            var effectiveDueDate = dueDateUtc ?? task.DueDate;

            // Validate hours
            ValidateHours(request.EstimatedHours, effectiveStartDate, effectiveDueDate, "EstimatedHours");
            ValidateHours(request.ActualHours, effectiveStartDate, effectiveDueDate, "ActualHours");

            // ============================================================
            // Preload assignees for validation and response
            // ============================================================
            var existingAssignments = await taskAssignmentRepository.GetAssigneesByTaskId(taskId);
            var oldAssigneeId = existingAssignments.FirstOrDefault()?.AssignedTo;

            var userIdsToLoad = new List<Guid?>();
            userIdsToLoad.Add(request.AssigneeId); // New assignee
            userIdsToLoad.Add(oldAssigneeId); // Old assignee
            var validUserIds = userIdsToLoad.Where(id => id.HasValue && id.Value != Guid.Empty).Select(id => id!.Value).Distinct().ToList();
            var userDict = new Dictionary<Guid, User>();
            if (validUserIds.Count > 0)
            {
                var users = await userRepository.GetByIdsAsync(validUserIds);
                userDict = users.ToDictionary(u => u.UserId);
            }

            // Resolve the user who will be credited for task completion
            var completionCreditedUserId = userId;
            if (request.AssigneeId.HasValue && request.AssigneeId.Value != Guid.Empty)
            {
                completionCreditedUserId = request.AssigneeId.Value;
            }
            else if (oldAssigneeId.HasValue)
            {
                completionCreditedUserId = oldAssigneeId.Value;
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

            bool reachedCompletion = false;
            string? oldStatusName = null;
            string? newStatusName = null;

            // Handle progress update
            if (request.Progress.HasValue)
            {
                var oldProgress = task.Progress;
                task.Progress = request.Progress.Value;

                reachedCompletion = oldProgress < 100 && task.Progress >= 100;
                var reopenedTask = oldProgress >= 100 && task.Progress < 100;

                if (task.Progress >= 100 && !task.CompletedAt.HasValue)
                {
                    task.CompletedAt = DateTime.UtcNow;
                }
                else if (reopenedTask)
                {
                    task.CompletedAt = null;
                }

                if (reachedCompletion)
                {
                    await activityLogService.LogTaskCompleteAsync(
                        completionCreditedUserId, task.TaskId, task.GroupId,
                        (int)task.Priority, (int)task.Severity);
                }

                await activityLogService.LogTaskUpdateAsync(
                    userId, task.TaskId, task.GroupId, null,
                    (int)task.Priority, (int)task.Severity);
            }

            // Handle status update
            if (request.GroupStatusId.HasValue)
            {
                if (task.GroupStatusId.HasValue && statusMap.TryGetValue(task.GroupStatusId.Value, out var oldStatus))
                {
                    oldStatusName = oldStatus.StatusName;
                }

                if (statusMap.TryGetValue(request.GroupStatusId.Value, out var newStatus))
                {
                    newStatusName = newStatus.StatusName;
                }

                task.GroupStatusId = request.GroupStatusId.Value;
            }

            // Update hours
            if (request.EstimatedHours.HasValue) task.EstimatedHours = request.EstimatedHours.Value;
            if (request.ActualHours.HasValue) task.ActualHours = request.ActualHours.Value;

            // ============================================================
            // DB write
            // ============================================================
            await taskRepository.UpdateAsync(task);
            // ============================================================
            // Handle assignee changes (DB writes)
            // ============================================================
            // Unassign: null or Guid.Empty both trigger removal
            if (!request.AssigneeId.HasValue || request.AssigneeId.Value == Guid.Empty)
            {
                if (existingAssignments.Any())
                {
                    await taskAssignmentRepository.RemoveAsync(existingAssignments);
                }
            }
            else
            {
                // Validate new assignee
                if (!userDict.TryGetValue(request.AssigneeId.Value, out var newAssignee) || newAssignee == null)
                {
                    throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
                }

                var alreadyAssigned = existingAssignments.Any(a => a.AssignedTo == request.AssigneeId.Value);
                if (!alreadyAssigned)
                {
                    if (existingAssignments.Any())
                        await taskAssignmentRepository.RemoveAsync(existingAssignments);

                    await taskAssignmentRepository.AddAsync(new TaskAssignment
                    {
                        AssignmentId = Guid.NewGuid(),
                        AssignedTo = request.AssigneeId.Value,
                        AssignedBy = userId,
                        AssignedAt = DateTime.UtcNow,
                        TaskId = taskId
                    });
                }
            }

            await taskUpdateNotificationQueue.EnqueueAsync(new TaskUpdateNotificationJob
            {
                TaskId = task.TaskId,
                GroupId = groupId,
                ActorUserId = userId,
                TaskTitle = task.Title,
                DueDate = task.DueDate,
                OldAssigneeId = oldAssigneeId,
                RequestedAssigneeId = request.AssigneeId.HasValue && request.AssigneeId.Value != Guid.Empty
                    ? request.AssigneeId.Value
                    : null,
                HasAssigneeUpdate = request.AssigneeId.HasValue || !existingAssignments.IsNullOrEmpty(),
                ReachedCompletion = reachedCompletion,
                OldStatusName = oldStatusName,
                NewStatusName = newStatusName
            });

            // ============================================================
            // Prepare response
            // ============================================================
            GroupTaskStatusDto? groupStatusDto = null;
            if (task.GroupStatusId.HasValue && statusMap.TryGetValue(task.GroupStatusId.Value, out var groupStatus))
            {
                groupStatusDto = new GroupTaskStatusDto
                {
                    GroupId = groupId,
                    StatusName = groupStatus.StatusName,
                    Position = groupStatus.Position
                };
            }

            UserDto assigneeDetail = new UserDto();
            if (oldAssigneeId.HasValue && userDict.TryGetValue(oldAssigneeId.Value, out var assigneeUser) && assigneeUser != null)
            {
                assigneeDetail = new UserDto
                {
                    Id = assigneeUser.UserId,
                    FirstName = assigneeUser.FirstName,
                    LastName = assigneeUser.LastName
                };
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
                GroupStatus = groupStatusDto,
                Assignee = assigneeDetail,
                EstimatedHours = task.EstimatedHours,
                ActualHours = task.ActualHours,
                CompletedAt = task.CompletedAt,
            };
        }

        public async Task SoftDeleteTaskAsync(Guid userId, Guid groupId, Guid taskId)
        {
            var userRole = await participantRepository.GetGroupRoleByUserIdAsync(userId, groupId);
            if (!userRole.Equals(GroupRole.Owner) && !userRole.Equals(GroupRole.Moderator) && !userRole.Equals(GroupRole.Member))
            {
                throw new AppException(ErrorCodes.GroupDeleteTaskDenined, StatusCodes.Status403Forbidden);
            }

            var group = await groupRepository.GetByIdAsync(groupId);
            if (group != null && group.IsArchived && userRole != GroupRole.Owner)
            {
                throw new AppException(ErrorCodes.GroupIsArchived, StatusCodes.Status403Forbidden);
            }

            var task = await taskRepository.GetByIdAsync(taskId);
            if (task == null)
            {
                throw new AppException(ErrorCodes.TaskNotFound, StatusCodes.Status404NotFound);
            }

            // Capture priority/severity before delete for weighted contribution scoring
            var taskPriority = (int)task.Priority;
            var taskSeverity = (int)task.Severity;

            await taskRepository.SoftDeleteAsync(taskId);

            await activityLogService.LogTaskDeleteAsync(userId, taskId, groupId, taskPriority, taskSeverity);

            // Notify Owner/Moderator of the group (parallel, using preloaded users)
            var participants = await participantRepository.GetAllByGroupIdAsync(groupId);
            var ownerModeratorIds = participants
                .Where(p => p.Role == GroupRole.Owner || p.Role == GroupRole.Moderator)
                .Select(p => p.UserId)
                .Where(id => id != userId)
                .Distinct()
                .ToList();

            var currentUser = await userRepository.GetByIdAsync(userId);
            if (ownerModeratorIds.Count > 0 && currentUser != null)
            {
                var omUsers = await userRepository.GetByIdsAsync(ownerModeratorIds);
                var tasks = omUsers
                    .Select(om => notificationService.NotifyTaskDeletedAsync(om, currentUser, taskId, task.Title))
                    .ToList();
                if (tasks.Count > 0)
                    await Task.WhenAll(tasks);
            }
        }

        public async Task DeletePersonalTaskAsync(Guid userId, Guid taskId)
        {
            var user = await userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status403Forbidden);
            }

            var task = await taskRepository.GetByIdAsync(taskId);
            if (task == null || task.OwnerId != userId || task.GroupId.HasValue)
            {
                throw new AppException(ErrorCodes.TaskNotFound, StatusCodes.Status404NotFound);
            }

            await taskRepository.PermanentDeleteAsync(taskId);
        }

        public async Task RestoreGroupTaskAsync(Guid userId, Guid groupId, Guid taskId)
        {
            var userRole = await participantRepository.GetGroupRoleByUserIdAsync(userId, groupId);
            if (!userRole.Equals(GroupRole.Owner) && !userRole.Equals(GroupRole.Moderator))
            {
                throw new AppException(ErrorCodes.GroupRestoreTaskDenined, StatusCodes.Status401Unauthorized);
            }

            var group = await groupRepository.GetByIdAsync(groupId);
            if (group != null && group.IsArchived && userRole != GroupRole.Owner)
            {
                throw new AppException(ErrorCodes.GroupIsArchived, StatusCodes.Status403Forbidden);
            }

            var task = await taskRepository.GetDeletedByIdAsync(taskId);

            if (task == null)
            {
                throw new AppException(ErrorCodes.TaskNotFound, StatusCodes.Status404NotFound);
            }

            var statusList = await groupTaskStatusRepository.GetByGroupIdAsync(groupId);

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
                var existingTask = await taskRepository.GetAllTasksByStatusIdAsync(firstStatus.StatusId);
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
                var statusExistTask = await taskRepository.GetAllTasksByStatusIdAsync(task.GroupStatusId.Value);
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
            await taskRepository.RestoreAsync(task);
        }
        public async Task<List<TaskDeleteResponse>> GetDeleteTaskListAsync(Guid userId, Guid groupId)
        {
            var existedUser = await participantRepository.GetByGroupAndUserAsync(groupId, userId);
            if (existedUser == null)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            var taskList = await taskRepository.GetSoftDeleteTaskByGroup(groupId);
            var taskListId = taskList.Select(t => t.TaskId).ToList();
            var taskDeleteLogs = await activityLogService.GetTaskDeleteLogsAsync(taskListId);

            var result = taskDeleteLogs.Select(t => new TaskDeleteResponse
            {
                DeleteTaskId = t.TargetId!.Value,
                TaskName = taskList.FirstOrDefault(task => task.TaskId == t.TargetId)?.Title ?? "Unknown Task",
                DeletedOn = t.CreatedAt,
                DeletedBy = t.UserId
            }).ToList();

            return result;
        }

        public async Task ReorderTaskAsync(Guid userId, Guid groupId, ReorderTaskRequest request)
        {
            var userRole = await participantRepository.GetGroupRoleByUserIdAsync(userId, groupId);
            if (userRole.Equals(GroupRole.Viewer) || userRole.Equals(GroupRole.Commenter))
            {
                throw new AppException(ErrorCodes.GroupUpdatePermissionDenied, StatusCodes.Status403Forbidden);
            }
            var task = await taskRepository.GetByIdAsync(request.TaskId);
            if (task == null)
            {
                throw new AppException(ErrorCodes.TaskNotFound, StatusCodes.Status404NotFound);
            }

            await taskRepository.ReorderTaskAsync(
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

            var user = await userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
            }

            if (!request.PersonalStatusId.HasValue)
            {
                throw new AppException(ErrorCodes.PersonalCreateTaskDeniedMissingStatus);
            }

            var personalTaskStatus = await personalTaskStatusRepository.GetDetailAsync(request.PersonalStatusId.Value);
            if (personalTaskStatus == null || personalTaskStatus.UserId != userId)
            {
                throw new AppException(ErrorCodes.StatusNotFound, StatusCodes.Status404NotFound);
            }

            var existingTask = await taskRepository.GetAllPersonalTasksByStatusIdAsync(request.PersonalStatusId.Value);
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
                    throw new AppException(ErrorCodes.TaskDateTimeError);
                }
            }

            // Validate hours
            ValidateHours(request.EstimatedHours, startDateUtc, dueDateUtc, "EstimatedHours");
            ValidateHours(request.ActualHours, startDateUtc, dueDateUtc, "ActualHours");

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
                EstimatedHours = request.EstimatedHours,
                ActualHours = request.ActualHours,
                Progress = 0,
                IsPendingDeleted = false,
                CreatedAt = now,
                UpdatedAt = now,
            };

            await taskRepository.AddAsync(taskItem);

            // Log task creation activity with priority/severity for weighted contribution scoring
            await activityLogService.LogTaskCreateAsync(
                userId, taskItem.TaskId, null, null,
                (int)taskItem.Priority, (int)taskItem.Severity);

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
                    AvatarUrl = AvatarUrlHelper.BuildAbsoluteAvatarUrl(user.AvatarUrl, httpContextAccessor.HttpContext)
                },
                EstimatedHours = taskItem.EstimatedHours,
                ActualHours = taskItem.ActualHours,
            };
        }
        public async Task<TaskItemResponse> UpdatePersonalTaskAsync(Guid userId, Guid taskId, UpdatePersonalTaskRequest request)
        {
            var user = await userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
            }

            var task = await taskRepository.GetByIdAsync(taskId);
            if (task == null || task.OwnerId != userId || task.GroupId.HasValue)
            {
                throw new AppException(ErrorCodes.TaskNotFound, StatusCodes.Status404NotFound);
            }

            // Validate PersonalStatusId if provided
            if (request.PersonalStatusId.HasValue)
            {
                var statusExists = await personalTaskStatusRepository.GetDetailAsync(request.PersonalStatusId.Value);
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
                    throw new AppException(ErrorCodes.TaskDateTimeError);
                }
            }

            // Effective dates for hours validation: use request values or existing task values
            var effectiveStartDate = startDateUtc ?? task.StartDate;
            var effectiveDueDate = dueDateUtc ?? task.DueDate;

            // Validate hours
            ValidateHours(request.EstimatedHours, effectiveStartDate, effectiveDueDate, "EstimatedHours");
            ValidateHours(request.ActualHours, effectiveStartDate, effectiveDueDate, "ActualHours");

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
                var oldProgress = task.Progress;
                task.Progress = request.Progress.Value;

                var reachedCompletion = oldProgress < 100 && task.Progress >= 100;
                var reopenedTask = oldProgress >= 100 && task.Progress < 100;

                // Keep CompletedAt in sync with progress state.
                if (task.Progress >= 100 && !task.CompletedAt.HasValue)
                {
                    task.CompletedAt = DateTime.UtcNow;
                }
                else if (reopenedTask)
                {
                    task.CompletedAt = null;
                }

                // Log task completion when progress reaches 100
                if (reachedCompletion)
                {
                    // Log with priority/severity for weighted contribution scoring
                    await activityLogService.LogTaskCompleteAsync(
                        userId, task.TaskId, task.GroupId,
                        (int)task.Priority, (int)task.Severity);
                }

                // Log task update activity for contribution scoring
                await activityLogService.LogTaskUpdateAsync(
                    userId, task.TaskId, task.GroupId, null,
                    (int)task.Priority, (int)task.Severity);
            }

            // Update personal if provided
            if (request.PersonalStatusId.HasValue)
            {
                task.PersonalStatusId = request.PersonalStatusId.Value;
            }

            // Update hours if provided
            if (request.EstimatedHours.HasValue)
            {
                task.EstimatedHours = request.EstimatedHours.Value;
            }

            if (request.ActualHours.HasValue)
            {
                task.ActualHours = request.ActualHours.Value;
            }

            await taskRepository.UpdateAsync(task);

            // Prepare response
            var personalStatus = task.PersonalStatusId.HasValue
                ? await personalTaskStatusRepository.GetDetailAsync(task.PersonalStatusId.Value)
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
                    AvatarUrl = AvatarUrlHelper.BuildAbsoluteAvatarUrl(user.AvatarUrl, httpContextAccessor.HttpContext)
                },
                EstimatedHours = task.EstimatedHours,
                ActualHours = task.ActualHours,
                CompletedAt = task.CompletedAt,
            };
        }
        public async Task ReorderPersonalTaskAsync(Guid userId, ReorderTaskRequest request)
        {
            var user = await userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
            }

            var task = await taskRepository.GetByIdAsync(request.TaskId);
            if (task == null || task.OwnerId != userId || task.GroupId.HasValue)
            {
                throw new AppException(ErrorCodes.TaskNotFound, StatusCodes.Status404NotFound);
            }

            var targetStatus = await personalTaskStatusRepository.GetDetailAsync(request.TargetStatusId);
            if (targetStatus == null || targetStatus.UserId != userId)
            {
                throw new AppException(ErrorCodes.StatusNotFound, StatusCodes.Status404NotFound);
            }

            if (request.PrevTaskId.HasValue)
            {
                var prevTask = await taskRepository.GetByIdAsync(request.PrevTaskId.Value);
                if (prevTask == null || prevTask.OwnerId != userId || prevTask.GroupId.HasValue || prevTask.PersonalStatusId != request.TargetStatusId)
                {
                    throw new AppException(ErrorCodes.TaskNotFound, StatusCodes.Status404NotFound);
                }
            }

            if (request.NextTaskId.HasValue)
            {
                var nextTask = await taskRepository.GetByIdAsync(request.NextTaskId.Value);
                if (nextTask == null || nextTask.OwnerId != userId || nextTask.GroupId.HasValue || nextTask.PersonalStatusId != request.TargetStatusId)
                {
                    throw new AppException(ErrorCodes.TaskNotFound, StatusCodes.Status404NotFound);
                }
            }

            await taskRepository.ReorderPersonalTaskAsync(
                request.TaskId,
                request.TargetStatusId,
                request.PrevTaskId,
                request.NextTaskId);
        }

        public async Task PermanentDeleteGroupTaskAsync(Guid userId, Guid groupId, Guid taskId)
        {
            var userRole = await participantRepository.GetGroupRoleByUserIdAsync(userId, groupId);
            if (!userRole.Equals(GroupRole.Owner) && !userRole.Equals(GroupRole.Moderator))
            {
                throw new AppException(ErrorCodes.GroupDeleteTaskDenined, StatusCodes.Status403Forbidden);
            }

            var task = await taskRepository.GetDeletedByIdAsync(taskId);
            if (task == null || task.GroupId != groupId)
            {
                throw new AppException(ErrorCodes.TaskNotFound, StatusCodes.Status404NotFound);
            }

            if (!task.IsPendingDeleted)
            {
                throw new AppException(ErrorCodes.TaskNotPendingDeleted);
            }

            await taskRepository.PermanentDeleteAsync(taskId);

        }

        /// <summary>
        /// Get the group ID for a given task (used for task deep-link URL resolution)
        /// Returns null if task not found or is soft-deleted
        /// </summary>
        public async Task<TaskGroupResponse?> GetTaskGroupAsync(Guid taskId, Guid userId)
        {
            var task = await taskRepository.GetByIdAsync(taskId);
            if (task?.GroupId == null)
            {
                return null;
            }

            var groupId = task.GroupId.Value;
            if (!await participantRepository.IsUserApprovedInGroupAsync(groupId, userId))
            {
                return null;
            }

            return new TaskGroupResponse
            {
                GroupId = groupId
            };
        }
    }
}
