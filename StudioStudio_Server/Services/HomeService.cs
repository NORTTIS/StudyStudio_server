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

        public async Task<HomeTaskResponse> GetGroupAssignedTaskAsync(Guid userId)
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

            // Get assigned task list
            var taskAssignList = await _assignmentRepository.GetListTaskIdByUserIdAsync(userId);
            var taskIdList = taskAssignList.Select(x => x.TaskId);

            return new HomeTaskResponse
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
                }).ToList(),
                GroupTaskAssigned = new List<AssignedGroupResponse>()
            };
        }

        public async Task<PersonalTaskStatusResponse> CreateNewGroupTaskStatus(Guid userId, PersonalTaskStatusRequest request)
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
                throw new AppException(ErrorCodes.GroupStatusNotFound, StatusCodes.Status404NotFound);
            }
            var taskList = await _taskRepository.GetAllTasksByStatusIdAsync(taskStatusId);
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
