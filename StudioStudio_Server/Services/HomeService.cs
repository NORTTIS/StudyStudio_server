using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    public class HomeService : IHomeService
    {
        private readonly ITaskAssignmentRepository _assignmentRepository;
        private readonly ITaskRepository _taskRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IGroupTaskStatusRepository _groupTaskStatusRepository;
        private readonly IPersonalTaskStatusRepository _personalTaskStatusRepository;
        private readonly IUserRepository _userRepository;

        public HomeService(
            ITaskAssignmentRepository assignmentRepository,
            ITaskRepository taskRepository,
            IGroupRepository groupRepository,
            IGroupTaskStatusRepository groupTaskStatusRepository,
            IPersonalTaskStatusRepository personalTaskStatusRepository,
            IUserRepository userRepository)
        {
            _assignmentRepository = assignmentRepository;
            _taskRepository = taskRepository;
            _groupRepository = groupRepository;
            _groupTaskStatusRepository = groupTaskStatusRepository;
            _personalTaskStatusRepository = personalTaskStatusRepository;
            _userRepository = userRepository;
        }

        /// <summary>
        /// Get personal task board data for the current user.
        /// Returns: PersonalTaskStatuses with tasks grouped by status.
        /// </summary>
        public async Task<PersonalTaskBoardResponse> GetPersonalTaskBoardAsync(Guid userId)
        {
            var userDetail = await _userRepository.GetByIdAsync(userId);
            if (userDetail == null)
            {
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
            }

            // Get personal status list
            var personalTaskStatus = await _personalTaskStatusRepository.GetAllByUserIdAsync(userId);
            var personalStatusIdList = personalTaskStatus.Select(s => s.StatusId).ToList();

            // Get user task list
            var personalTaskList = await _taskRepository.GetPersonalListTasksByListStatusId(personalStatusIdList);

            return new PersonalTaskBoardResponse
            {
                PersonalTaskStatuses = personalTaskStatus.Select(pt => new TaskStatusDto
                {
                    StatusId = pt.StatusId,
                    StatusName = pt.StatusName,
                    Position = pt.Position,
                    TaskList = personalTaskList.TryGetValue(pt.StatusId, out var tasks)
                         ? tasks.Select(t => new TaskItemResponse
                         {
                             TaskId = t.TaskId,
                             TaskTitle = t.Title,
                             TaskDescription = t.Description,
                             TaskPriority = t.Priority,
                             TaskSeverity = t.Severity,
                             Position = t.Position,
                             Progress = t.Progress,
                             CreatedById = t.OwnerId,
                             CreatedAt = t.CreatedAt,
                             StartDate = t.StartDate,
                             DueDate = t.DueDate,
                             Assignee = new UserDto
                             {
                                 Id = userId,
                                 FirstName = userDetail.FirstName,
                                 LastName = userDetail.LastName,
                                 AvatarUrl = userDetail.AvatarUrl,
                             }
                         }).ToList()
                         : new List<TaskItemResponse>()
                }).ToList()
            };
        }

        /// <summary>
        /// Get home summary metrics including remaining, overdue, completed tasks and joined groups.
        /// </summary>
        public async Task<HomeSummaryResponse> GetHomeSummaryAsync(Guid userId)
        {
            await EnsureUserExistsAsync(userId);

            var personalTasks = await _taskRepository.GetPersonalTasksByOwnerAsync(userId);
            var groupTasks = await _taskRepository.GetAssignedGroupTasksByUserAsync(userId);
            var allTasks = personalTasks.Concat(groupTasks).ToList();

            var completedTaskCount = allTasks.Count(IsTaskCompleted);
            var overdueTaskCount = allTasks.Count(t => t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow && !IsTaskCompleted(t));
            var remainingTaskCount = allTasks.Count - completedTaskCount;

            var userGroups = await _groupRepository.GetUserGroupsAsync(userId);

            return new HomeSummaryResponse
            {
                RemainingTaskCount = remainingTaskCount,
                OverdueTaskCount = overdueTaskCount,
                CompletedTaskCount = completedTaskCount,
                TotalJoinedGroupCount = userGroups.Count
            };
        }

        /// <summary>
        /// Get assigned group task list with pagination, search, filter, and sort.
        /// Only returns group tasks assigned to the user (excludes personal tasks).
        /// Uses database-level pagination for better performance.
        /// </summary>
        public async Task<HomeTaskListResponse> GetHomeTaskListAsync(
            Guid userId,
            int page,
            int pageSize,
            string? search = null,
            Guid? groupId = null,
            string? sortBy = "asc")
        {
            await EnsureUserExistsAsync(userId);

            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 10 : pageSize;

            // Determine sort order
            bool sortAscending = sortBy?.ToLower() != "desc";

            // Get tasks with database-level pagination
            var (groupTasks, totalCount) = await _taskRepository.GetAssignedGroupTasksWithPaginationAsync(
                userId, page, pageSize, search, groupId, sortAscending);

            // Get user groups for the response
            var userGroups = await _groupRepository.GetUserGroupsAsync(userId);
            var userGroupDtos = userGroups.Select(g => new UserGroupDto
            {
                GroupId = g.GroupId,
                GroupName = g.GroupName
            }).ToList();

            // Convert to response items
            var taskItems = groupTasks.Select(t => new HomeTaskListItemResponse
            {
                TaskId = t.TaskId,
                TaskTitle = t.Title,
                SourceType = "Group",
                SourceName = t.Group?.GroupName ?? "Group",
                GroupId = t.GroupId,
                StatusName = t.GroupStatus?.StatusName ?? string.Empty,
                TaskSeverity = t.Severity,
                TaskPriority = t.Priority,
                Progress = t.Progress,
                DueDate = t.DueDate
            }).ToList();

            // Calculate total pages
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);

            return new HomeTaskListResponse
            {
                Items = taskItems,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                UserGroups = userGroupDtos
            };
        }

        /// <summary>
        /// Validate that the user exists.
        /// </summary>
        private async Task EnsureUserExistsAsync(Guid userId)
        {
            var userDetail = await _userRepository.GetByIdAsync(userId);
            if (userDetail == null)
            {
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
            }
        }

        /// <summary>
        /// Determine whether a task is completed based on progress or status name.
        /// </summary>
        private static bool IsTaskCompleted(TaskItem task)
        {
            return task.Progress >= 100;

        }

        public async Task<PersonalTaskStatusResponse> CreateNewPersonalTaskStatus(Guid userId, PersonalTaskStatusRequest request)
        {
            var userDetail = await _userRepository.GetByIdAsync(userId);
            if (userDetail == null)
            {
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
            }

            var existingStatuses = await _personalTaskStatusRepository.GetAllByUserIdAsync(userId);

            int newPosition;
            if (existingStatuses.Any())
            {
                newPosition = existingStatuses.Max(s => s.Position) + 1000;
            }
            else
            {
                newPosition = 1000;
            }

            var newStatus = new PersonalTaskStatus
            {
                StatusId = Guid.NewGuid(),
                UserId = userId,
                StatusName = request.StatusName,
                Position = newPosition,
                CreatedAt = DateTime.UtcNow,
            };

            if (await _personalTaskStatusRepository.IsNameExist(newStatus))
            {
                throw new AppException(ErrorCodes.StatusNameExist, StatusCodes.Status400BadRequest);
            }

            await _personalTaskStatusRepository.AddAsync(newStatus);

            return new PersonalTaskStatusResponse
            {
                StatusId = newStatus.StatusId,
                StatusName = newStatus.StatusName,
                Position = newPosition,
            };
        }
        public async Task DeletePersonalTaskStatus(Guid userId, Guid taskStatusId)
        {
            var userDetail = await _userRepository.GetByIdAsync(userId);
            if (userDetail == null)
            {
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
            }
            var taskStatus = await _personalTaskStatusRepository.GetDetailAsync(taskStatusId);
            if (taskStatus == null || taskStatus.UserId != userId)
            {
                throw new AppException(ErrorCodes.StatusNotFound, StatusCodes.Status404NotFound);
            }
            var taskList = await _taskRepository.GetAllPersonalTasksByStatusIdAsync(taskStatusId);
            if (taskList.Any())
            {
                throw new AppException(ErrorCodes.GroupDeleteTaskStatusFailed, StatusCodes.Status400BadRequest);
            }
            await _personalTaskStatusRepository.DeletePersonalStatusAsync(taskStatus);
        }
        public async Task UpdatePersonalTaskStatus(Guid userId, Guid taskStatusId, PersonalTaskStatusRequest request)
        {
            var userDetail = await _userRepository.GetByIdAsync(userId);
            if (userDetail == null)
            {
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
            }

            var taskStatus = await _personalTaskStatusRepository.GetDetailAsync(taskStatusId);
            if (taskStatus == null || taskStatus.UserId != userId)
            {
                throw new AppException(ErrorCodes.StatusNotFound, StatusCodes.Status404NotFound);
            }

            taskStatus.StatusName = request.StatusName;

            if (await _personalTaskStatusRepository.IsNameExist(taskStatus))
            {
                throw new AppException(ErrorCodes.StatusNameExist, StatusCodes.Status400BadRequest);
            }

            await _personalTaskStatusRepository.UpdatePersonalStatusAsync(taskStatus);
        }

        public async Task ReorderPersonalTaskStatus(Guid userId, ReorderPersonalTaskStatusRequest request)
        {
            var userDetail = await _userRepository.GetByIdAsync(userId);
            if (userDetail == null)
            {
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
            }

            var status = await _personalTaskStatusRepository.GetDetailAsync(request.StatusId);
            if (status == null || status.UserId != userId)
            {
                throw new AppException(ErrorCodes.StatusNotFound, StatusCodes.Status404NotFound);
            }

            await _personalTaskStatusRepository.ReorderStatusAsync(
                request.StatusId,
                request.PrevStatusId,
                request.NextStatusId,
                userId
            );
        }
    }
}
