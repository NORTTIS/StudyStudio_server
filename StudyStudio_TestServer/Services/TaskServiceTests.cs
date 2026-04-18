using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services;
using StudioStudio_Server.Services.Interfaces;
using StudioStudio_Server.Services.TaskNotificationQueue;
using Xunit;

namespace StudioStudio_Server.Tests.Services
{
    /// <summary>
    /// Unit tests cho TaskService.
    /// Tests: task CRUD, personal/group tasks, assignments, status management.
    /// Ref: Services/TaskService.cs
    /// </summary>
    public class TaskServiceTests
    {
        private readonly Mock<ILogger<TaskService>> _loggerMock;
        private readonly Mock<IMessageService> _messageServiceMock;
        private readonly Mock<ITaskRepository> _taskRepoMock;
        private readonly Mock<IGroupRepository> _groupRepoMock;
        private readonly Mock<IGroupParticipantRepository> _participantRepoMock;
        private readonly Mock<IGroupTaskStatusRepository> _groupTaskStatusRepoMock;
        private readonly Mock<ITaskAssignmentRepository> _taskAssignmentRepoMock;
        private readonly Mock<IUserRepository> _userRepoMock;
        private readonly Mock<IPersonalTaskStatusRepository> _personalTaskStatusRepoMock;
        private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
        private readonly Mock<IActivityLogService> _activityLogMock;
        private readonly Mock<INotificationService> _notificationMock;
        private readonly Mock<ITaskUpdateNotificationQueue> _notificationQueueMock;
        private TaskService _service = null!;

        // Fixed test IDs
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _groupId = Guid.NewGuid();
        private readonly Guid _taskId = Guid.NewGuid();
        private readonly Guid _statusId = Guid.NewGuid();
        private readonly Guid _assigneeId = Guid.NewGuid();
        private readonly Guid _personalStatusId = Guid.NewGuid();

        public TaskServiceTests()
        {
            _loggerMock = new Mock<ILogger<TaskService>>();
            _messageServiceMock = new Mock<IMessageService>();
            _taskRepoMock = new Mock<ITaskRepository>();
            _groupRepoMock = new Mock<IGroupRepository>();
            _participantRepoMock = new Mock<IGroupParticipantRepository>();
            _groupTaskStatusRepoMock = new Mock<IGroupTaskStatusRepository>();
            _taskAssignmentRepoMock = new Mock<ITaskAssignmentRepository>();
            _userRepoMock = new Mock<IUserRepository>();
            _personalTaskStatusRepoMock = new Mock<IPersonalTaskStatusRepository>();
            _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
            _activityLogMock = new Mock<IActivityLogService>();
            _notificationMock = new Mock<INotificationService>();
            _notificationQueueMock = new Mock<ITaskUpdateNotificationQueue>();
            InitService();
        }

        private void InitService()
        {
            _service = new TaskService(
                _taskRepoMock.Object,
                _loggerMock.Object,
                _messageServiceMock.Object,
                _groupRepoMock.Object,
                _participantRepoMock.Object,
                _groupTaskStatusRepoMock.Object,
                _taskAssignmentRepoMock.Object,
                _userRepoMock.Object,
                _personalTaskStatusRepoMock.Object,
                _httpContextAccessorMock.Object,
                _activityLogMock.Object,
                _notificationMock.Object,
                _notificationQueueMock.Object);
        }

        #region AddGroupTaskAsync

        [Fact]
        public async Task AddGroupTaskAsync_ViewerRole_ThrowsForbidden()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Viewer);

            var request = new TaskItemGroupRequest
            {
                GroupId = _groupId,
                TaskName = "Test",
                TaskPriority = TaskPriority.Medium,
                TaskSeverity = TaskSeverity.Moderate,
                GroupStatusId = _statusId
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.AddGroupTaskAsync(_userId, request));
            Assert.Equal(ErrorCodes.GroupCreateTaskDenied, ex.Code);
            Assert.Equal(403, ex.HttpStatus);
        }

        [Fact]
        public async Task AddGroupTaskAsync_CommenterRole_ThrowsForbidden()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Commenter);

            var request = new TaskItemGroupRequest
            {
                GroupId = _groupId,
                TaskName = "Test",
                TaskPriority = TaskPriority.Medium,
                TaskSeverity = TaskSeverity.Moderate,
                GroupStatusId = _statusId
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.AddGroupTaskAsync(_userId, request));
            Assert.Equal(ErrorCodes.GroupCreateTaskDenied, ex.Code);
        }

        [Fact]
        public async Task AddGroupTaskAsync_ArchivedGroupNonOwner_ThrowsForbidden()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId))
                .ReturnsAsync(new Group { GroupId = _groupId, IsArchived = true });

            var request = new TaskItemGroupRequest
            {
                GroupId = _groupId,
                TaskName = "Test",
                TaskPriority = TaskPriority.Medium,
                TaskSeverity = TaskSeverity.Moderate,
                GroupStatusId = _statusId
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.AddGroupTaskAsync(_userId, request));
            Assert.Equal(ErrorCodes.GroupIsArchived, ex.Code);
        }

        [Fact]
        public async Task AddGroupTaskAsync_ArchivedGroupOwner_Succeeds()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Owner);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId))
                .ReturnsAsync(new Group { GroupId = _groupId, IsArchived = true });

            var groupStatus = new GroupTaskStatus { StatusId = _statusId, GroupId = _groupId, StatusName = "To Do", Position = 1000 };
            _groupTaskStatusRepoMock.Setup(x => x.GetDetailAsync(_statusId))
                .ReturnsAsync(groupStatus);
            _taskRepoMock.Setup(x => x.GetAllTasksByStatusIdAsync(_statusId))
                .ReturnsAsync(new List<TaskItem>());

            var request = new TaskItemGroupRequest
            {
                GroupId = _groupId,
                TaskName = "Test Task",
                TaskPriority = TaskPriority.Medium,
                TaskSeverity = TaskSeverity.Moderate,
                GroupStatusId = _statusId
            };

            // Act
            var result = await _service.AddGroupTaskAsync(_userId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Test Task", result.TaskTitle);
            Assert.Equal(TaskPriority.Medium, result.TaskPriority);
        }

        [Fact]
        public async Task AddGroupTaskAsync_MissingStatusId_ThrowsBadRequest()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId))
                .ReturnsAsync(new Group { GroupId = _groupId, IsArchived = false });

            var request = new TaskItemGroupRequest
            {
                GroupId = _groupId,
                TaskName = "Test",
                TaskPriority = TaskPriority.Medium,
                TaskSeverity = TaskSeverity.Moderate,
                GroupStatusId = null
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.AddGroupTaskAsync(_userId, request));
            Assert.Equal(ErrorCodes.GroupCreateTaskDeniedMissingStatus, ex.Code);
        }

        [Fact]
        public async Task AddGroupTaskAsync_InvalidPriority_ThrowsBadRequest()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);

            var request = new TaskItemGroupRequest
            {
                GroupId = _groupId,
                TaskName = "Test",
                TaskPriority = (TaskPriority)999,
                TaskSeverity = TaskSeverity.Moderate,
                GroupStatusId = _statusId
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.AddGroupTaskAsync(_userId, request));
            Assert.Equal(ErrorCodes.TaskInvalidPriority, ex.Code);
        }

        [Fact]
        public async Task AddGroupTaskAsync_InvalidSeverity_ThrowsBadRequest()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);

            var request = new TaskItemGroupRequest
            {
                GroupId = _groupId,
                TaskName = "Test",
                TaskPriority = TaskPriority.Medium,
                TaskSeverity = (TaskSeverity)999,
                GroupStatusId = _statusId
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.AddGroupTaskAsync(_userId, request));
            Assert.Equal(ErrorCodes.TaskInvalidSeverity, ex.Code);
        }

        [Fact]
        public async Task AddGroupTaskAsync_StartDateAfterDueDate_ThrowsBadRequest()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId))
                .ReturnsAsync(new Group { GroupId = _groupId, IsArchived = false });

            var groupStatus = new GroupTaskStatus { StatusId = _statusId, GroupId = _groupId, StatusName = "To Do", Position = 1000 };
            _groupTaskStatusRepoMock.Setup(x => x.GetDetailAsync(_statusId))
                .ReturnsAsync(groupStatus);
            _taskRepoMock.Setup(x => x.GetAllTasksByStatusIdAsync(_statusId))
                .ReturnsAsync(new List<TaskItem>());

            var request = new TaskItemGroupRequest
            {
                GroupId = _groupId,
                TaskName = "Test",
                TaskPriority = TaskPriority.Medium,
                TaskSeverity = TaskSeverity.Moderate,
                GroupStatusId = _statusId,
                StartDate = new DateTime(2026, 4, 20),
                DueDate = new DateTime(2026, 4, 10)
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.AddGroupTaskAsync(_userId, request));
            Assert.Equal(ErrorCodes.TaskDateTimeError, ex.Code);
        }

        [Fact]
        public async Task AddGroupTaskAsync_EstimatedHoursExceedDuration_ThrowsBadRequest()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId))
                .ReturnsAsync(new Group { GroupId = _groupId, IsArchived = false });

            var groupStatus = new GroupTaskStatus { StatusId = _statusId, GroupId = _groupId, StatusName = "To Do", Position = 1000 };
            _groupTaskStatusRepoMock.Setup(x => x.GetDetailAsync(_statusId))
                .ReturnsAsync(groupStatus);
            _taskRepoMock.Setup(x => x.GetAllTasksByStatusIdAsync(_statusId))
                .ReturnsAsync(new List<TaskItem>());

            var request = new TaskItemGroupRequest
            {
                GroupId = _groupId,
                TaskName = "Test",
                TaskPriority = TaskPriority.Medium,
                TaskSeverity = TaskSeverity.Moderate,
                GroupStatusId = _statusId,
                StartDate = new DateTime(2026, 4, 10),
                DueDate = new DateTime(2026, 4, 11), // 24 hours total
                EstimatedHours = 48 // exceeds 24 hours
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.AddGroupTaskAsync(_userId, request));
            Assert.Contains("EstimatedHours", ex.Code);
        }

        [Fact]
        public async Task AddGroupTaskAsync_ValidRequest_ReturnsTaskItem()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId))
                .ReturnsAsync(new Group { GroupId = _groupId, IsArchived = false });

            var groupStatus = new GroupTaskStatus { StatusId = _statusId, GroupId = _groupId, StatusName = "To Do", Position = 1000 };
            _groupTaskStatusRepoMock.Setup(x => x.GetDetailAsync(_statusId))
                .ReturnsAsync(groupStatus);
            _taskRepoMock.Setup(x => x.GetAllTasksByStatusIdAsync(_statusId))
                .ReturnsAsync(new List<TaskItem>());

            var request = new TaskItemGroupRequest
            {
                GroupId = _groupId,
                TaskName = "New Task",
                TaskDescription = "Description",
                TaskPriority = TaskPriority.High,
                TaskSeverity = TaskSeverity.Critical,
                GroupStatusId = _statusId,
                EstimatedHours = 8
            };

            // Act
            var result = await _service.AddGroupTaskAsync(_userId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("New Task", result.TaskTitle);
            Assert.Equal(TaskPriority.High, result.TaskPriority);
            Assert.Equal(TaskSeverity.Critical, result.TaskSeverity);
            Assert.Equal(1000, result.Position);
            _taskRepoMock.Verify(x => x.AddAsync(It.IsAny<TaskItem>()), Times.Once);
            _activityLogMock.Verify(x => x.LogTaskCreateAsync(_userId, It.IsAny<Guid>(), _groupId, null, (int)TaskPriority.High, (int)TaskSeverity.Critical), Times.Once);
        }

        [Fact]
        public async Task AddGroupTaskAsync_WithAssignee_CreatesAssignmentAndNotifies()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId))
                .ReturnsAsync(new Group { GroupId = _groupId, IsArchived = false });

            var groupStatus = new GroupTaskStatus { StatusId = _statusId, GroupId = _groupId, StatusName = "To Do", Position = 1000 };
            _groupTaskStatusRepoMock.Setup(x => x.GetDetailAsync(_statusId))
                .ReturnsAsync(groupStatus);
            _taskRepoMock.Setup(x => x.GetAllTasksByStatusIdAsync(_statusId))
                .ReturnsAsync(new List<TaskItem>());

            var assignee = new User { UserId = _assigneeId, FirstName = "Bob", LastName = "Smith" };
            var currentUser = new User { UserId = _userId, FirstName = "Alice", LastName = "Wonder" };
            _userRepoMock.Setup(x => x.GetByIdAsync(_assigneeId)).ReturnsAsync(assignee);
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(currentUser);

            var request = new TaskItemGroupRequest
            {
                GroupId = _groupId,
                TaskName = "Task with Assignee",
                TaskPriority = TaskPriority.Medium,
                TaskSeverity = TaskSeverity.Moderate,
                GroupStatusId = _statusId,
                Assignees = _assigneeId
            };

            // Act
            var result = await _service.AddGroupTaskAsync(_userId, request);

            // Assert
            Assert.NotNull(result);
            _taskAssignmentRepoMock.Verify(x => x.AddAsync(It.IsAny<TaskAssignment>()), Times.Once);
            _activityLogMock.Verify(x => x.LogTaskAssignAsync(_userId, It.IsAny<Guid>(), _assigneeId, _groupId), Times.Once);
            _notificationMock.Verify(x => x.NotifyTaskAssignedAsync(assignee, currentUser, It.IsAny<Guid>(), "Task with Assignee", null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task AddGroupTaskAsync_AssigneeNotFound_ThrowsNotFound()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId))
                .ReturnsAsync(new Group { GroupId = _groupId, IsArchived = false });

            var groupStatus = new GroupTaskStatus { StatusId = _statusId, GroupId = _groupId, StatusName = "To Do", Position = 1000 };
            _groupTaskStatusRepoMock.Setup(x => x.GetDetailAsync(_statusId))
                .ReturnsAsync(groupStatus);
            _taskRepoMock.Setup(x => x.GetAllTasksByStatusIdAsync(_statusId))
                .ReturnsAsync(new List<TaskItem>());

            _userRepoMock.Setup(x => x.GetByIdAsync(_assigneeId)).ReturnsAsync((User?)null);

            var request = new TaskItemGroupRequest
            {
                GroupId = _groupId,
                TaskName = "Test",
                TaskPriority = TaskPriority.Medium,
                TaskSeverity = TaskSeverity.Moderate,
                GroupStatusId = _statusId,
                Assignees = _assigneeId
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.AddGroupTaskAsync(_userId, request));
            Assert.Equal(ErrorCodes.UserNotFound, ex.Code);
        }

        #endregion

        #region UpdateGroupTaskAsync

        [Fact]
        public async Task UpdateGroupTaskAsync_ViewerRole_ThrowsForbidden()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Viewer);

            var request = new UpdateTaskRequest { TaskName = "Updated" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateGroupTaskAsync(_userId, _groupId, _taskId, request));
            Assert.Equal(ErrorCodes.GroupUpdatePermissionDenied, ex.Code);
        }

        [Fact]
        public async Task UpdateGroupTaskAsync_ArchivedGroupNonOwner_ThrowsForbidden()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId))
                .ReturnsAsync(new Group { GroupId = _groupId, IsArchived = true });

            var request = new UpdateTaskRequest { TaskName = "Updated" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateGroupTaskAsync(_userId, _groupId, _taskId, request));
            Assert.Equal(ErrorCodes.GroupIsArchived, ex.Code);
        }

        [Fact]
        public async Task UpdateGroupTaskAsync_TaskNotFound_ThrowsNotFound()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId))
                .ReturnsAsync(new Group { GroupId = _groupId, IsArchived = false });
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId))
                .ReturnsAsync((TaskItem?)null);

            var request = new UpdateTaskRequest { TaskName = "Updated" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateGroupTaskAsync(_userId, _groupId, _taskId, request));
            Assert.Equal(ErrorCodes.TaskNotFound, ex.Code);
        }

        [Fact]
        public async Task UpdateGroupTaskAsync_TaskBelongsToDifferentGroup_ThrowsNotFound()
        {
            // Arrange
            var otherGroupId = Guid.NewGuid();
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId))
                .ReturnsAsync(new Group { GroupId = _groupId, IsArchived = false });
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId))
                .ReturnsAsync(new TaskItem { TaskId = _taskId, GroupId = otherGroupId });

            var request = new UpdateTaskRequest { TaskName = "Updated" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateGroupTaskAsync(_userId, _groupId, _taskId, request));
            Assert.Equal(ErrorCodes.TaskNotFound, ex.Code);
        }

        [Fact]
        public async Task UpdateGroupTaskAsync_InvalidStatusId_ThrowsNotFound()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId))
                .ReturnsAsync(new Group { GroupId = _groupId, IsArchived = false });

            var task = new TaskItem { TaskId = _taskId, GroupId = _groupId, GroupStatusId = _statusId };
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId)).ReturnsAsync(task);
            _groupTaskStatusRepoMock.Setup(x => x.GetByIdsAndGroupIdAsync(It.IsAny<List<Guid>>(), _groupId))
                .ReturnsAsync(new List<GroupTaskStatus>());

            var newStatusId = Guid.NewGuid();
            var request = new UpdateTaskRequest { GroupStatusId = newStatusId };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateGroupTaskAsync(_userId, _groupId, _taskId, request));
            Assert.Equal(ErrorCodes.GroupStatusNotFound, ex.Code);
        }

        [Fact]
        public async Task UpdateGroupTaskAsync_ValidRequest_UpdatesTaskAndEnqueuesNotification()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId))
                .ReturnsAsync(new Group { GroupId = _groupId, IsArchived = false });

            var task = new TaskItem
            {
                TaskId = _taskId,
                GroupId = _groupId,
                GroupStatusId = _statusId,
                Title = "Old Title",
                Priority = TaskPriority.Low,
                Severity = TaskSeverity.Minor,
                Progress = 0,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId)).ReturnsAsync(task);

            var groupStatus = new GroupTaskStatus { StatusId = _statusId, GroupId = _groupId, StatusName = "In Progress", Position = 2000 };
            _groupTaskStatusRepoMock.Setup(x => x.GetByIdsAndGroupIdAsync(It.IsAny<List<Guid>>(), _groupId))
                .ReturnsAsync(new List<GroupTaskStatus> { groupStatus });

            _taskAssignmentRepoMock.Setup(x => x.GetAssigneesByTaskId(_taskId))
                .ReturnsAsync(new List<TaskAssignment>());

            var request = new UpdateTaskRequest
            {
                TaskName = "Updated Task",
                Progress = 50,
                GroupStatusId = _statusId
            };

            // Act
            var result = await _service.UpdateGroupTaskAsync(_userId, _groupId, _taskId, request);

            // Assert
            Assert.Equal("Updated Task", result.TaskTitle);
            Assert.Equal(50, result.Progress);
            _taskRepoMock.Verify(x => x.UpdateAsync(It.IsAny<TaskItem>()), Times.Once);
            _activityLogMock.Verify(x => x.LogTaskUpdateAsync(_userId, _taskId, _groupId, null, (int)TaskPriority.Low, (int)TaskSeverity.Minor, null, null, null), Times.Once);
            _notificationQueueMock.Verify(x => x.EnqueueAsync(It.IsAny<TaskUpdateNotificationJob>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateGroupTaskAsync_ProgressReaches100_LogsCompletion()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId))
                .ReturnsAsync(new Group { GroupId = _groupId, IsArchived = false });

            var task = new TaskItem
            {
                TaskId = _taskId,
                GroupId = _groupId,
                GroupStatusId = _statusId,
                Title = "Test",
                Priority = TaskPriority.Medium,
                Severity = TaskSeverity.Moderate,
                Progress = 50,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId)).ReturnsAsync(task);

            _taskAssignmentRepoMock.Setup(x => x.GetAssigneesByTaskId(_taskId))
                .ReturnsAsync(new List<TaskAssignment>());
            _groupTaskStatusRepoMock.Setup(x => x.GetByIdsAndGroupIdAsync(It.IsAny<List<Guid>>(), _groupId))
                .ReturnsAsync(new List<GroupTaskStatus>());

            var request = new UpdateTaskRequest { Progress = 100 };

            // Act
            var result = await _service.UpdateGroupTaskAsync(_userId, _groupId, _taskId, request);

            // Assert
            Assert.Equal(100, result.Progress);
            Assert.NotNull(result.CompletedAt);
            _activityLogMock.Verify(x => x.LogTaskCompleteAsync(_userId, _taskId, _groupId, (int)TaskPriority.Medium, (int)TaskSeverity.Moderate), Times.Once);
        }

        [Fact]
        public async Task UpdateGroupTaskAsync_ReopenTask_ClearsCompletedAt()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId))
                .ReturnsAsync(new Group { GroupId = _groupId, IsArchived = false });

            var task = new TaskItem
            {
                TaskId = _taskId,
                GroupId = _groupId,
                GroupStatusId = _statusId,
                Title = "Test",
                Priority = TaskPriority.Medium,
                Severity = TaskSeverity.Moderate,
                Progress = 100,
                CompletedAt = DateTime.UtcNow.AddHours(-1),
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId)).ReturnsAsync(task);

            _taskAssignmentRepoMock.Setup(x => x.GetAssigneesByTaskId(_taskId))
                .ReturnsAsync(new List<TaskAssignment>());
            _groupTaskStatusRepoMock.Setup(x => x.GetByIdsAndGroupIdAsync(It.IsAny<List<Guid>>(), _groupId))
                .ReturnsAsync(new List<GroupTaskStatus>());

            var request = new UpdateTaskRequest { Progress = 50 };

            // Act
            var result = await _service.UpdateGroupTaskAsync(_userId, _groupId, _taskId, request);

            // Assert
            Assert.Equal(50, result.Progress);
            Assert.Null(result.CompletedAt);
        }

        [Fact]
        public async Task UpdateGroupTaskAsync_UnassignAssignee_RemovesAssignment()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId))
                .ReturnsAsync(new Group { GroupId = _groupId, IsArchived = false });

            var task = new TaskItem
            {
                TaskId = _taskId,
                GroupId = _groupId,
                GroupStatusId = _statusId,
                Title = "Test",
                Priority = TaskPriority.Low,
                Severity = TaskSeverity.Minor,
                Progress = 0,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId)).ReturnsAsync(task);

            var existingAssignment = new TaskAssignment { AssignmentId = Guid.NewGuid(), AssignedTo = _assigneeId, TaskId = _taskId };
            _taskAssignmentRepoMock.Setup(x => x.GetAssigneesByTaskId(_taskId))
                .ReturnsAsync(new List<TaskAssignment> { existingAssignment });
            _groupTaskStatusRepoMock.Setup(x => x.GetByIdsAndGroupIdAsync(It.IsAny<List<Guid>>(), _groupId))
                .ReturnsAsync(new List<GroupTaskStatus>());
            _userRepoMock.Setup(x => x.GetByIdsAsync(It.IsAny<List<Guid>>()))
                .ReturnsAsync(new List<User>());

            var request = new UpdateTaskRequest { AssigneeId = null };

            // Act
            await _service.UpdateGroupTaskAsync(_userId, _groupId, _taskId, request);

            // Assert
            _taskAssignmentRepoMock.Verify(x => x.RemoveAsync(It.Is<List<TaskAssignment>>(l => l.Count == 1)), Times.Once);
        }

        [Fact]
        public async Task UpdateGroupTaskAsync_ChangeAssignee_RemovesOldAndAddsNew()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId))
                .ReturnsAsync(new Group { GroupId = _groupId, IsArchived = false });

            var task = new TaskItem
            {
                TaskId = _taskId,
                GroupId = _groupId,
                GroupStatusId = _statusId,
                Title = "Test",
                Priority = TaskPriority.Low,
                Severity = TaskSeverity.Minor,
                Progress = 0,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId)).ReturnsAsync(task);

            var oldAssignment = new TaskAssignment { AssignmentId = Guid.NewGuid(), AssignedTo = _assigneeId, TaskId = _taskId };
            _taskAssignmentRepoMock.Setup(x => x.GetAssigneesByTaskId(_taskId))
                .ReturnsAsync(new List<TaskAssignment> { oldAssignment });
            _groupTaskStatusRepoMock.Setup(x => x.GetByIdsAndGroupIdAsync(It.IsAny<List<Guid>>(), _groupId))
                .ReturnsAsync(new List<GroupTaskStatus>());

            var newAssigneeId = Guid.NewGuid();
            var newAssignee = new User { UserId = newAssigneeId, FirstName = "New", LastName = "User" };
            _userRepoMock.Setup(x => x.GetByIdsAsync(It.IsAny<List<Guid>>()))
                .ReturnsAsync(new List<User> { newAssignee });

            var request = new UpdateTaskRequest { AssigneeId = newAssigneeId };

            // Act
            await _service.UpdateGroupTaskAsync(_userId, _groupId, _taskId, request);

            // Assert
            _taskAssignmentRepoMock.Verify(x => x.RemoveAsync(It.Is<List<TaskAssignment>>(l => l.Count == 1)), Times.Once);
            _taskAssignmentRepoMock.Verify(x => x.AddAsync(It.IsAny<TaskAssignment>()), Times.Once);
        }

        [Fact]
        public async Task UpdateGroupTaskAsync_NewAssigneeNotFound_ThrowsNotFound()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId))
                .ReturnsAsync(new Group { GroupId = _groupId, IsArchived = false });

            var task = new TaskItem
            {
                TaskId = _taskId,
                GroupId = _groupId,
                GroupStatusId = _statusId,
                Title = "Test",
                Priority = TaskPriority.Low,
                Severity = TaskSeverity.Minor,
                Progress = 0,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId)).ReturnsAsync(task);

            _taskAssignmentRepoMock.Setup(x => x.GetAssigneesByTaskId(_taskId))
                .ReturnsAsync(new List<TaskAssignment>());
            _groupTaskStatusRepoMock.Setup(x => x.GetByIdsAndGroupIdAsync(It.IsAny<List<Guid>>(), _groupId))
                .ReturnsAsync(new List<GroupTaskStatus>());

            var newAssigneeId = Guid.NewGuid();
            _userRepoMock.Setup(x => x.GetByIdsAsync(It.IsAny<List<Guid>>()))
                .ReturnsAsync(new List<User>());

            var request = new UpdateTaskRequest { AssigneeId = newAssigneeId };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateGroupTaskAsync(_userId, _groupId, _taskId, request));
            Assert.Equal(ErrorCodes.UserNotFound, ex.Code);
        }

        #endregion

        #region SoftDeleteTaskAsync

       
        [Fact]
        public async Task SoftDeleteTaskAsync_ViewerRole_ThrowsForbidden()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Viewer);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.SoftDeleteTaskAsync(_userId, _groupId, _taskId));
            Assert.Equal(ErrorCodes.GroupDeleteTaskDenined, ex.Code);
        }

        [Fact]
        public async Task SoftDeleteTaskAsync_ArchivedGroupNonOwner_ThrowsForbidden()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId))
                .ReturnsAsync(new Group { GroupId = _groupId, IsArchived = true });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.SoftDeleteTaskAsync(_userId, _groupId, _taskId));
            Assert.Equal(ErrorCodes.GroupIsArchived, ex.Code);
        }

        [Fact]
        public async Task SoftDeleteTaskAsync_TaskNotFound_ThrowsNotFound()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId))
                .ReturnsAsync(new Group { GroupId = _groupId, IsArchived = false });
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId))
                .ReturnsAsync((TaskItem?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.SoftDeleteTaskAsync(_userId, _groupId, _taskId));
            Assert.Equal(ErrorCodes.TaskNotFound, ex.Code);
        }

        [Fact]
        public async Task SoftDeleteTaskAsync_ValidOwner_CallsSoftDeleteAndNotifies()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Owner);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId))
                .ReturnsAsync(new Group { GroupId = _groupId, IsArchived = false });

            var task = new TaskItem
            {
                TaskId = _taskId,
                GroupId = _groupId,
                Title = "Task to Delete",
                Priority = TaskPriority.High,
                Severity = TaskSeverity.Critical
            };
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId)).ReturnsAsync(task);

            var participants = new List<GroupParticipant>
            {
                new() { UserId = _userId, Role = GroupRole.Owner },
                new() { UserId = Guid.NewGuid(), Role = GroupRole.Moderator }
            };
            _participantRepoMock.Setup(x => x.GetAllByGroupIdAsync(_groupId))
                .ReturnsAsync(participants);

            var currentUser = new User { UserId = _userId, FirstName = "Owner", LastName = "User" };
            var modUser = new User { UserId = participants[1].UserId, FirstName = "Mod", LastName = "User" };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(currentUser);
            _userRepoMock.Setup(x => x.GetByIdsAsync(It.Is<List<Guid>>(l => l.Count == 1 && l[0] == modUser.UserId)))
                .ReturnsAsync(new List<User> { modUser });

            // Act
            await _service.SoftDeleteTaskAsync(_userId, _groupId, _taskId);

            // Assert
            _taskRepoMock.Verify(x => x.SoftDeleteAsync(_taskId), Times.Once);
            _activityLogMock.Verify(x => x.LogTaskDeleteAsync(_userId, _taskId, _groupId, (int)TaskPriority.High, (int)TaskSeverity.Critical), Times.Once);
            _notificationMock.Verify(x => x.NotifyTaskDeletedAsync(modUser, currentUser, _taskId, "Task to Delete", It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region RestoreGroupTaskAsync

        [Fact]
        public async Task RestoreGroupTaskAsync_MemberRole_ThrowsUnauthorized()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Member);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.RestoreGroupTaskAsync(_userId, _groupId, _taskId));
            Assert.Equal(ErrorCodes.GroupRestoreTaskDenined, ex.Code);
        }

        [Fact]
        public async Task RestoreGroupTaskAsync_ArchivedGroupNonOwner_ThrowsForbidden()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId))
                .ReturnsAsync(new Group { GroupId = _groupId, IsArchived = true });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.RestoreGroupTaskAsync(_userId, _groupId, _taskId));
            Assert.Equal(ErrorCodes.GroupIsArchived, ex.Code);
        }

        [Fact]
        public async Task RestoreGroupTaskAsync_DeletedTaskNotFound_ThrowsNotFound()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId))
                .ReturnsAsync(new Group { GroupId = _groupId, IsArchived = false });
            _taskRepoMock.Setup(x => x.GetDeletedByIdAsync(_taskId))
                .ReturnsAsync((TaskItem?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.RestoreGroupTaskAsync(_userId, _groupId, _taskId));
            Assert.Equal(ErrorCodes.TaskNotFound, ex.Code);
        }

        [Fact]
        public async Task RestoreGroupTaskAsync_AllStatusDeleted_ThrowsForbidden()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId))
                .ReturnsAsync(new Group { GroupId = _groupId, IsArchived = false });

            var task = new TaskItem { TaskId = _taskId, GroupId = _groupId, IsPendingDeleted = true };
            _taskRepoMock.Setup(x => x.GetDeletedByIdAsync(_taskId)).ReturnsAsync(task);
            _groupTaskStatusRepoMock.Setup(x => x.GetByGroupIdAsync(_groupId))
                .ReturnsAsync(new List<GroupTaskStatus>());

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.RestoreGroupTaskAsync(_userId, _groupId, _taskId));
            Assert.Equal(ErrorCodes.GroupRestoreTaskFailed, ex.Code);
        }

        [Fact]
        public async Task RestoreGroupTaskAsync_WithoutOldStatusId_AssignsFirstStatus()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId))
                .ReturnsAsync(new Group { GroupId = _groupId, IsArchived = false });

            var firstStatus = new GroupTaskStatus { StatusId = _statusId, GroupId = _groupId, StatusName = "To Do", Position = 1000 };
            var task = new TaskItem
            {
                TaskId = _taskId,
                GroupId = _groupId,
                GroupStatusId = null,
                IsPendingDeleted = true,
                Position = 0
            };
            _taskRepoMock.Setup(x => x.GetDeletedByIdAsync(_taskId)).ReturnsAsync(task);
            _groupTaskStatusRepoMock.Setup(x => x.GetByGroupIdAsync(_groupId))
                .ReturnsAsync(new List<GroupTaskStatus> { firstStatus });
            _taskRepoMock.Setup(x => x.GetAllTasksByStatusIdAsync(_statusId))
                .ReturnsAsync(new List<TaskItem>());

            // Act
            await _service.RestoreGroupTaskAsync(_userId, _groupId, _taskId);

            // Assert
            Assert.Equal(_statusId, task.GroupStatusId);
            Assert.Equal(1000, task.Position);
            _taskRepoMock.Verify(x => x.RestoreAsync(task), Times.Once);
        }

        [Fact]
        public async Task RestoreGroupTaskAsync_WithOldStatusId_AssignsSameStatus()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId))
                .ReturnsAsync(new Group { GroupId = _groupId, IsArchived = false });

            var task = new TaskItem
            {
                TaskId = _taskId,
                GroupId = _groupId,
                GroupStatusId = _statusId,
                IsPendingDeleted = true,
                Position = 0
            };
            _taskRepoMock.Setup(x => x.GetDeletedByIdAsync(_taskId)).ReturnsAsync(task);
            _groupTaskStatusRepoMock.Setup(x => x.GetByGroupIdAsync(_groupId))
                .ReturnsAsync(new List<GroupTaskStatus>
                {
                    new() { StatusId = _statusId, GroupId = _groupId, StatusName = "To Do", Position = 1000 }
                });
            _taskRepoMock.Setup(x => x.GetAllTasksByStatusIdAsync(_statusId))
                .ReturnsAsync(new List<TaskItem> { new TaskItem { Position = 2000 } });

            // Act
            await _service.RestoreGroupTaskAsync(_userId, _groupId, _taskId);

            // Assert
            Assert.Equal(_statusId, task.GroupStatusId);
            Assert.Equal(3000, task.Position); // existing max + 1000
            _taskRepoMock.Verify(x => x.RestoreAsync(task), Times.Once);
        }

        #endregion

        #region GetDeleteTaskListAsync

        [Fact]
        public async Task GetDeleteTaskListAsync_UserNotInGroup_ThrowsForbidden()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetByGroupAndUserAsync(_groupId, _userId))
                .ReturnsAsync((GroupParticipant?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.GetDeleteTaskListAsync(_userId, _groupId));
            Assert.Equal(ErrorCodes.AuthForbidden, ex.Code);
        }

        [Fact]
        public async Task GetDeleteTaskListAsync_ValidMember_ReturnsTaskDeleteResponses()
        {
            // Arrange
            var participant = new GroupParticipant { UserId = _userId, GroupId = _groupId, Role = GroupRole.Member };
            _participantRepoMock.Setup(x => x.GetByGroupAndUserAsync(_groupId, _userId))
                .ReturnsAsync(participant);

            var deletedTask = new TaskItem { TaskId = _taskId, Title = "Deleted Task", GroupId = _groupId };
            _taskRepoMock.Setup(x => x.GetSoftDeleteTaskByGroup(_groupId))
                .ReturnsAsync(new List<TaskItem> { deletedTask });

            var activityLog = new StudioStudio_Server.Models.Entities.ActivityLog
            {
                LogId = Guid.NewGuid(),
                UserId = _userId,
                TargetType = "Task",
                TargetId = _taskId,
                ActionType = "TaskDelete",
                CreatedAt = DateTime.UtcNow
            };
            _activityLogMock.Setup(x => x.GetTaskDeleteLogsAsync(It.IsAny<List<Guid>>()))
                .ReturnsAsync(new List<StudioStudio_Server.Models.Entities.ActivityLog> { activityLog });

            // Act
            var result = await _service.GetDeleteTaskListAsync(_userId, _groupId);

            // Assert
            Assert.Single(result);
            Assert.Equal(_taskId, result[0].DeleteTaskId);
            Assert.Equal("Deleted Task", result[0].TaskName);
        }

        [Fact]
        public async Task GetDeleteTaskListAsync_NoDeletedTasks_ReturnsEmptyList()
        {
            // Arrange
            var participant = new GroupParticipant { UserId = _userId, GroupId = _groupId, Role = GroupRole.Member };
            _participantRepoMock.Setup(x => x.GetByGroupAndUserAsync(_groupId, _userId))
                .ReturnsAsync(participant);
            _taskRepoMock.Setup(x => x.GetSoftDeleteTaskByGroup(_groupId))
                .ReturnsAsync(new List<TaskItem>());
            _activityLogMock.Setup(x => x.GetTaskDeleteLogsAsync(It.IsAny<List<Guid>>()))
                .ReturnsAsync(new List<StudioStudio_Server.Models.Entities.ActivityLog>());

            // Act
            var result = await _service.GetDeleteTaskListAsync(_userId, _groupId);

            // Assert
            Assert.Empty(result);
        }

        #endregion

        #region ReorderTaskAsync

        [Fact]
        public async Task ReorderTaskAsync_ViewerRole_ThrowsForbidden()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Viewer);

            var request = new ReorderTaskRequest { TaskId = _taskId, TargetStatusId = _statusId };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ReorderTaskAsync(_userId, _groupId, request));
            Assert.Equal(ErrorCodes.GroupUpdatePermissionDenied, ex.Code);
        }

        [Fact]
        public async Task ReorderTaskAsync_TaskNotFound_ThrowsNotFound()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId))
                .ReturnsAsync((TaskItem?)null);

            var request = new ReorderTaskRequest { TaskId = _taskId, TargetStatusId = _statusId };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ReorderTaskAsync(_userId, _groupId, request));
            Assert.Equal(ErrorCodes.TaskNotFound, ex.Code);
        }

        [Fact]
        public async Task ReorderTaskAsync_ValidModerator_CallsReorderTask()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            var task = new TaskItem { TaskId = _taskId, GroupId = _groupId };
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId)).ReturnsAsync(task);

            var prevTaskId = Guid.NewGuid();
            var nextTaskId = Guid.NewGuid();
            var request = new ReorderTaskRequest
            {
                TaskId = _taskId,
                TargetStatusId = _statusId,
                PrevTaskId = prevTaskId,
                NextTaskId = nextTaskId
            };

            // Act
            await _service.ReorderTaskAsync(_userId, _groupId, request);

            // Assert
            _taskRepoMock.Verify(x => x.ReorderTaskAsync(_taskId, _statusId, prevTaskId, nextTaskId), Times.Once);
        }

        #endregion

        #region AddPersonalTaskAsync

        [Fact]
        public async Task AddPersonalTaskAsync_InvalidPriority_ThrowsBadRequest()
        {
            // Arrange
            var request = new TaskItemPersonalRequest
            {
                TaskName = "Test",
                TaskPriority = (TaskPriority)999,
                TaskSeverity = TaskSeverity.Moderate,
                PersonalStatusId = _personalStatusId
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.AddPersonalTaskAsync(_userId, request));
            Assert.Equal(ErrorCodes.TaskInvalidPriority, ex.Code);
        }

        [Fact]
        public async Task AddPersonalTaskAsync_UserNotFound_ThrowsNotFound()
        {
            // Arrange
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId))
                .ReturnsAsync((User?)null);

            var request = new TaskItemPersonalRequest
            {
                TaskName = "Test",
                TaskPriority = TaskPriority.Medium,
                TaskSeverity = TaskSeverity.Moderate,
                PersonalStatusId = _personalStatusId
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.AddPersonalTaskAsync(_userId, request));
            Assert.Equal(ErrorCodes.UserNotFound, ex.Code);
        }

        [Fact]
        public async Task AddPersonalTaskAsync_MissingStatusId_ThrowsBadRequest()
        {
            // Arrange
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId))
                .ReturnsAsync(new User { UserId = _userId });

            var request = new TaskItemPersonalRequest
            {
                TaskName = "Test",
                TaskPriority = TaskPriority.Medium,
                TaskSeverity = TaskSeverity.Moderate,
                PersonalStatusId = null
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.AddPersonalTaskAsync(_userId, request));
            Assert.Equal(ErrorCodes.PersonalCreateTaskDeniedMissingStatus, ex.Code);
        }

        [Fact]
        public async Task AddPersonalTaskAsync_StatusNotOwned_ThrowsNotFound()
        {
            // Arrange
            var otherUserId = Guid.NewGuid();
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId))
                .ReturnsAsync(new User { UserId = _userId });
            _personalTaskStatusRepoMock.Setup(x => x.GetDetailAsync(_personalStatusId))
                .ReturnsAsync(new PersonalTaskStatus { StatusId = _personalStatusId, UserId = otherUserId });

            var request = new TaskItemPersonalRequest
            {
                TaskName = "Test",
                TaskPriority = TaskPriority.Medium,
                TaskSeverity = TaskSeverity.Moderate,
                PersonalStatusId = _personalStatusId
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.AddPersonalTaskAsync(_userId, request));
            Assert.Equal(ErrorCodes.StatusNotFound, ex.Code);
        }

        [Fact]
        public async Task AddPersonalTaskAsync_StartDateAfterDueDate_ThrowsBadRequest()
        {
            // Arrange
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId))
                .ReturnsAsync(new User { UserId = _userId });
            _personalTaskStatusRepoMock.Setup(x => x.GetDetailAsync(_personalStatusId))
                .ReturnsAsync(new PersonalTaskStatus { StatusId = _personalStatusId, UserId = _userId });
            _taskRepoMock.Setup(x => x.GetAllPersonalTasksByStatusIdAsync(_personalStatusId))
                .ReturnsAsync(new List<TaskItem>());

            var request = new TaskItemPersonalRequest
            {
                TaskName = "Test",
                TaskPriority = TaskPriority.Medium,
                TaskSeverity = TaskSeverity.Moderate,
                PersonalStatusId = _personalStatusId,
                StartDate = new DateTime(2026, 4, 20),
                DueDate = new DateTime(2026, 4, 10)
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.AddPersonalTaskAsync(_userId, request));
            Assert.Equal(ErrorCodes.TaskDateTimeError, ex.Code);
        }

        [Fact]
        public async Task AddPersonalTaskAsync_ValidRequest_CreatesPersonalTask()
        {
            // Arrange
            var user = new User { UserId = _userId, FirstName = "John", LastName = "Doe" };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);
            _personalTaskStatusRepoMock.Setup(x => x.GetDetailAsync(_personalStatusId))
                .ReturnsAsync(new PersonalTaskStatus { StatusId = _personalStatusId, UserId = _userId, StatusName = "My Tasks", Position = 1000 });
            _taskRepoMock.Setup(x => x.GetAllPersonalTasksByStatusIdAsync(_personalStatusId))
                .ReturnsAsync(new List<TaskItem>());

            var httpContext = new DefaultHttpContext();
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

            var request = new TaskItemPersonalRequest
            {
                TaskName = "Personal Task",
                TaskDescription = "Description",
                TaskPriority = TaskPriority.High,
                TaskSeverity = TaskSeverity.Critical,
                PersonalStatusId = _personalStatusId,
                EstimatedHours = 4
            };

            // Act
            var result = await _service.AddPersonalTaskAsync(_userId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Personal Task", result.TaskTitle);
            Assert.Null(result.GroupStatus);
            Assert.NotNull(result.PersonalStatus);
            Assert.Equal("My Tasks", result.PersonalStatus.StatusName);
            Assert.NotNull(result.Assignee);
            Assert.Equal(_userId, result.Assignee.Id);
            _taskRepoMock.Verify(x => x.AddAsync(It.IsAny<TaskItem>()), Times.Once);
            _activityLogMock.Verify(x => x.LogTaskCreateAsync(_userId, It.IsAny<Guid>(), null, null, (int)TaskPriority.High, (int)TaskSeverity.Critical), Times.Once);
        }

        #endregion

        #region UpdatePersonalTaskAsync

        [Fact]
        public async Task UpdatePersonalTaskAsync_UserNotFound_ThrowsNotFound()
        {
            // Arrange
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId))
                .ReturnsAsync((User?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdatePersonalTaskAsync(_userId, _taskId, new UpdatePersonalTaskRequest()));
            Assert.Equal(ErrorCodes.UserNotFound, ex.Code);
        }

        [Fact]
        public async Task UpdatePersonalTaskAsync_TaskNotFound_ThrowsNotFound()
        {
            // Arrange
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId))
                .ReturnsAsync(new User { UserId = _userId });
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId))
                .ReturnsAsync((TaskItem?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdatePersonalTaskAsync(_userId, _taskId, new UpdatePersonalTaskRequest()));
            Assert.Equal(ErrorCodes.TaskNotFound, ex.Code);
        }

        [Fact]
        public async Task UpdatePersonalTaskAsync_TaskNotOwned_ThrowsNotFound()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId))
                .ReturnsAsync(new User { UserId = _userId });
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId))
                .ReturnsAsync(new TaskItem { TaskId = _taskId, OwnerId = ownerId, GroupId = null });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdatePersonalTaskAsync(_userId, _taskId, new UpdatePersonalTaskRequest()));
            Assert.Equal(ErrorCodes.TaskNotFound, ex.Code);
        }

        [Fact]
        public async Task UpdatePersonalTaskAsync_GroupTask_ThrowsNotFound()
        {
            // Arrange
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId))
                .ReturnsAsync(new User { UserId = _userId });
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId))
                .ReturnsAsync(new TaskItem { TaskId = _taskId, OwnerId = _userId, GroupId = _groupId });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdatePersonalTaskAsync(_userId, _taskId, new UpdatePersonalTaskRequest()));
            Assert.Equal(ErrorCodes.TaskNotFound, ex.Code);
        }

        [Fact]
        public async Task UpdatePersonalTaskAsync_InvalidPersonalStatusId_ThrowsNotFound()
        {
            // Arrange
            var user = new User { UserId = _userId, FirstName = "John", LastName = "Doe" };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId))
                .ReturnsAsync(new TaskItem { TaskId = _taskId, OwnerId = _userId, GroupId = null });

            var newStatusId = Guid.NewGuid();
            _personalTaskStatusRepoMock.Setup(x => x.GetDetailAsync(newStatusId))
                .ReturnsAsync((PersonalTaskStatus?)null);

            var request = new UpdatePersonalTaskRequest { PersonalStatusId = newStatusId };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdatePersonalTaskAsync(_userId, _taskId, request));
            Assert.Equal(ErrorCodes.StatusNotFound, ex.Code);
        }

        [Fact]
        public async Task UpdatePersonalTaskAsync_ValidRequest_UpdatesAndLogsCompletion()
        {
            // Arrange
            var user = new User { UserId = _userId, FirstName = "John", LastName = "Doe" };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);

            var task = new TaskItem
            {
                TaskId = _taskId,
                OwnerId = _userId,
                GroupId = null,
                PersonalStatusId = _personalStatusId,
                Title = "Old Title",
                Priority = TaskPriority.Low,
                Severity = TaskSeverity.Minor,
                Progress = 0
            };
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId)).ReturnsAsync(task);

            var personalStatus = new PersonalTaskStatus { StatusId = _personalStatusId, UserId = _userId, StatusName = "Done", Position = 2000 };
            _personalTaskStatusRepoMock.Setup(x => x.GetDetailAsync(_personalStatusId))
                .ReturnsAsync(personalStatus);

            var httpContext = new DefaultHttpContext();
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

            var request = new UpdatePersonalTaskRequest
            {
                TaskName = "Updated Title",
                Progress = 100,
                PersonalStatusId = _personalStatusId
            };

            // Act
            var result = await _service.UpdatePersonalTaskAsync(_userId, _taskId, request);

            // Assert
            Assert.Equal("Updated Title", result.TaskTitle);
            Assert.Equal(100, result.Progress);
            Assert.NotNull(result.CompletedAt);
            _taskRepoMock.Verify(x => x.UpdateAsync(It.IsAny<TaskItem>()), Times.Once);
            _activityLogMock.Verify(x => x.LogTaskCompleteAsync(_userId, _taskId, null, (int)TaskPriority.Low, (int)TaskSeverity.Minor), Times.Once);
            _activityLogMock.Verify(x => x.LogTaskUpdateAsync(_userId, _taskId, null, null, (int)TaskPriority.Low, (int)TaskSeverity.Minor, null, null, null), Times.Once);
        }

        [Fact]
        public async Task UpdatePersonalTaskAsync_ProgressReopen_ClearsCompletedAt()
        {
            // Arrange
            var user = new User { UserId = _userId, FirstName = "John", LastName = "Doe" };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);

            var task = new TaskItem
            {
                TaskId = _taskId,
                OwnerId = _userId,
                GroupId = null,
                PersonalStatusId = _personalStatusId,
                Title = "Test",
                Priority = TaskPriority.Medium,
                Severity = TaskSeverity.Moderate,
                Progress = 100,
                CompletedAt = DateTime.UtcNow.AddHours(-1)
            };
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId)).ReturnsAsync(task);
            _personalTaskStatusRepoMock.Setup(x => x.GetDetailAsync(_personalStatusId))
                .ReturnsAsync(new PersonalTaskStatus { StatusId = _personalStatusId, UserId = _userId, StatusName = "Tasks", Position = 1000 });

            var httpContext = new DefaultHttpContext();
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

            var request = new UpdatePersonalTaskRequest { Progress = 50 };

            // Act
            var result = await _service.UpdatePersonalTaskAsync(_userId, _taskId, request);

            // Assert
            Assert.Equal(50, result.Progress);
            Assert.Null(result.CompletedAt);
        }

        #endregion

        #region ReorderPersonalTaskAsync

        [Fact]
        public async Task ReorderPersonalTaskAsync_UserNotFound_ThrowsNotFound()
        {
            // Arrange
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId))
                .ReturnsAsync((User?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ReorderPersonalTaskAsync(_userId, new ReorderTaskRequest { TaskId = _taskId, TargetStatusId = _personalStatusId }));
            Assert.Equal(ErrorCodes.UserNotFound, ex.Code);
        }

        [Fact]
        public async Task ReorderPersonalTaskAsync_TaskNotOwned_ThrowsNotFound()
        {
            // Arrange
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId))
                .ReturnsAsync(new User { UserId = _userId });
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId))
                .ReturnsAsync(new TaskItem { TaskId = _taskId, OwnerId = Guid.NewGuid(), GroupId = null });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ReorderPersonalTaskAsync(_userId, new ReorderTaskRequest { TaskId = _taskId, TargetStatusId = _personalStatusId }));
            Assert.Equal(ErrorCodes.TaskNotFound, ex.Code);
        }

        [Fact]
        public async Task ReorderPersonalTaskAsync_TargetStatusNotOwned_ThrowsNotFound()
        {
            // Arrange
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId))
                .ReturnsAsync(new User { UserId = _userId });
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId))
                .ReturnsAsync(new TaskItem { TaskId = _taskId, OwnerId = _userId, GroupId = null });
            _personalTaskStatusRepoMock.Setup(x => x.GetDetailAsync(_personalStatusId))
                .ReturnsAsync(new PersonalTaskStatus { StatusId = _personalStatusId, UserId = Guid.NewGuid() });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ReorderPersonalTaskAsync(_userId, new ReorderTaskRequest { TaskId = _taskId, TargetStatusId = _personalStatusId }));
            Assert.Equal(ErrorCodes.StatusNotFound, ex.Code);
        }

        [Fact]
        public async Task ReorderPersonalTaskAsync_PrevTaskInvalid_ThrowsNotFound()
        {
            // Arrange
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId))
                .ReturnsAsync(new User { UserId = _userId });
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId))
                .ReturnsAsync(new TaskItem { TaskId = _taskId, OwnerId = _userId, GroupId = null, PersonalStatusId = _personalStatusId });
            _personalTaskStatusRepoMock.Setup(x => x.GetDetailAsync(_personalStatusId))
                .ReturnsAsync(new PersonalTaskStatus { StatusId = _personalStatusId, UserId = _userId });

            var prevTaskId = Guid.NewGuid();
            _taskRepoMock.Setup(x => x.GetByIdAsync(prevTaskId))
                .ReturnsAsync((TaskItem?)null);

            var request = new ReorderTaskRequest
            {
                TaskId = _taskId,
                TargetStatusId = _personalStatusId,
                PrevTaskId = prevTaskId
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ReorderPersonalTaskAsync(_userId, request));
            Assert.Equal(ErrorCodes.TaskNotFound, ex.Code);
        }

        [Fact]
        public async Task ReorderPersonalTaskAsync_NextTaskNotInTargetStatus_ThrowsNotFound()
        {
            // Arrange
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId))
                .ReturnsAsync(new User { UserId = _userId });

            var nextTaskId = Guid.NewGuid();
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId))
                .ReturnsAsync(new TaskItem { TaskId = _taskId, OwnerId = _userId, GroupId = null, PersonalStatusId = _personalStatusId });
            _personalTaskStatusRepoMock.Setup(x => x.GetDetailAsync(_personalStatusId))
                .ReturnsAsync(new PersonalTaskStatus { StatusId = _personalStatusId, UserId = _userId });
            _taskRepoMock.Setup(x => x.GetByIdAsync(nextTaskId))
                .ReturnsAsync(new TaskItem { TaskId = nextTaskId, OwnerId = _userId, GroupId = null, PersonalStatusId = Guid.NewGuid() });

            var request = new ReorderTaskRequest
            {
                TaskId = _taskId,
                TargetStatusId = _personalStatusId,
                NextTaskId = nextTaskId
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ReorderPersonalTaskAsync(_userId, request));
            Assert.Equal(ErrorCodes.TaskNotFound, ex.Code);
        }

        [Fact]
        public async Task ReorderPersonalTaskAsync_ValidRequest_CallsReorderPersonalTask()
        {
            // Arrange
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId))
                .ReturnsAsync(new User { UserId = _userId });

            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId))
                .ReturnsAsync(new TaskItem { TaskId = _taskId, OwnerId = _userId, GroupId = null, PersonalStatusId = _personalStatusId });
            _personalTaskStatusRepoMock.Setup(x => x.GetDetailAsync(_personalStatusId))
                .ReturnsAsync(new PersonalTaskStatus { StatusId = _personalStatusId, UserId = _userId });

            var request = new ReorderTaskRequest
            {
                TaskId = _taskId,
                TargetStatusId = _personalStatusId,
                PrevTaskId = null,
                NextTaskId = null
            };

            // Act
            await _service.ReorderPersonalTaskAsync(_userId, request);

            // Assert
            _taskRepoMock.Verify(x => x.ReorderPersonalTaskAsync(_taskId, _personalStatusId, null, null), Times.Once);
        }

        #endregion

        #region DeletePersonalTaskAsync

        [Fact]
        public async Task DeletePersonalTaskAsync_UserNotFound_ThrowsForbidden()
        {
            // Arrange
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId))
                .ReturnsAsync((User?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.DeletePersonalTaskAsync(_userId, _taskId));
            Assert.Equal(ErrorCodes.UserNotFound, ex.Code);
            Assert.Equal(403, ex.HttpStatus);
        }

        [Fact]
        public async Task DeletePersonalTaskAsync_TaskNotFound_ThrowsNotFound()
        {
            // Arrange
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId))
                .ReturnsAsync(new User { UserId = _userId });
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId))
                .ReturnsAsync((TaskItem?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.DeletePersonalTaskAsync(_userId, _taskId));
            Assert.Equal(ErrorCodes.TaskNotFound, ex.Code);
        }

        [Fact]
        public async Task DeletePersonalTaskAsync_TaskNotOwned_ThrowsNotFound()
        {
            // Arrange
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId))
                .ReturnsAsync(new User { UserId = _userId });
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId))
                .ReturnsAsync(new TaskItem { TaskId = _taskId, OwnerId = Guid.NewGuid(), GroupId = null });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.DeletePersonalTaskAsync(_userId, _taskId));
            Assert.Equal(ErrorCodes.TaskNotFound, ex.Code);
        }

        [Fact]
        public async Task DeletePersonalTaskAsync_IsGroupTask_ThrowsNotFound()
        {
            // Arrange
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId))
                .ReturnsAsync(new User { UserId = _userId });
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId))
                .ReturnsAsync(new TaskItem { TaskId = _taskId, OwnerId = _userId, GroupId = _groupId });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.DeletePersonalTaskAsync(_userId, _taskId));
            Assert.Equal(ErrorCodes.TaskNotFound, ex.Code);
        }

        [Fact]
        public async Task DeletePersonalTaskAsync_ValidRequest_CallsPermanentDelete()
        {
            // Arrange
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId))
                .ReturnsAsync(new User { UserId = _userId });
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId))
                .ReturnsAsync(new TaskItem { TaskId = _taskId, OwnerId = _userId, GroupId = null });

            // Act
            await _service.DeletePersonalTaskAsync(_userId, _taskId);

            // Assert
            _taskRepoMock.Verify(x => x.PermanentDeleteAsync(_taskId), Times.Once);
        }

        #endregion

        #region PermanentDeleteGroupTaskAsync

        [Fact]
        public async Task PermanentDeleteGroupTaskAsync_MemberRole_ThrowsForbidden()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Member);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.PermanentDeleteGroupTaskAsync(_userId, _groupId, _taskId));
            Assert.Equal(ErrorCodes.GroupDeleteTaskDenined, ex.Code);
        }

        [Fact]
        public async Task PermanentDeleteGroupTaskAsync_TaskNotFound_ThrowsNotFound()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _taskRepoMock.Setup(x => x.GetDeletedByIdAsync(_taskId))
                .ReturnsAsync((TaskItem?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.PermanentDeleteGroupTaskAsync(_userId, _groupId, _taskId));
            Assert.Equal(ErrorCodes.TaskNotFound, ex.Code);
        }

        [Fact]
        public async Task PermanentDeleteGroupTaskAsync_TaskBelongsToDifferentGroup_ThrowsNotFound()
        {
            // Arrange
            var otherGroupId = Guid.NewGuid();
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _taskRepoMock.Setup(x => x.GetDeletedByIdAsync(_taskId))
                .ReturnsAsync(new TaskItem { TaskId = _taskId, GroupId = otherGroupId, IsPendingDeleted = true });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.PermanentDeleteGroupTaskAsync(_userId, _groupId, _taskId));
            Assert.Equal(ErrorCodes.TaskNotFound, ex.Code);
        }

        [Fact]
        public async Task PermanentDeleteGroupTaskAsync_NotPendingDelete_ThrowsBadRequest()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _taskRepoMock.Setup(x => x.GetDeletedByIdAsync(_taskId))
                .ReturnsAsync(new TaskItem { TaskId = _taskId, GroupId = _groupId, IsPendingDeleted = false });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.PermanentDeleteGroupTaskAsync(_userId, _groupId, _taskId));
            Assert.Equal(ErrorCodes.TaskNotPendingDeleted, ex.Code);
        }

        [Fact]
        public async Task PermanentDeleteGroupTaskAsync_ValidModerator_CallsPermanentDelete()
        {
            // Arrange
            _participantRepoMock.Setup(x => x.GetGroupRoleByUserIdAsync(_userId, _groupId))
                .ReturnsAsync(GroupRole.Moderator);
            _taskRepoMock.Setup(x => x.GetDeletedByIdAsync(_taskId))
                .ReturnsAsync(new TaskItem { TaskId = _taskId, GroupId = _groupId, IsPendingDeleted = true });

            // Act
            await _service.PermanentDeleteGroupTaskAsync(_userId, _groupId, _taskId);

            // Assert
            _taskRepoMock.Verify(x => x.PermanentDeleteAsync(_taskId), Times.Once);
        }

        #endregion

        #region GetTaskGroupAsync

        [Fact]
        public async Task GetTaskGroupAsync_TaskNotFound_ReturnsNull()
        {
            // Arrange
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId))
                .ReturnsAsync((TaskItem?)null);

            // Act
            var result = await _service.GetTaskGroupAsync(_taskId, _userId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetTaskGroupAsync_PersonalTask_ReturnsNull()
        {
            // Arrange
            var task = new TaskItem { TaskId = _taskId, GroupId = null };
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId)).ReturnsAsync(task);

            // Act
            var result = await _service.GetTaskGroupAsync(_taskId, _userId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetTaskGroupAsync_UserNotInGroup_ReturnsNull()
        {
            // Arrange
            var task = new TaskItem { TaskId = _taskId, GroupId = _groupId };
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId)).ReturnsAsync(task);
            _participantRepoMock.Setup(x => x.IsUserApprovedInGroupAsync(_groupId, _userId))
                .ReturnsAsync(false);

            // Act
            var result = await _service.GetTaskGroupAsync(_taskId, _userId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetTaskGroupAsync_ValidUserInGroup_ReturnsGroupId()
        {
            // Arrange
            var task = new TaskItem { TaskId = _taskId, GroupId = _groupId };
            _taskRepoMock.Setup(x => x.GetByIdAsync(_taskId)).ReturnsAsync(task);
            _participantRepoMock.Setup(x => x.IsUserApprovedInGroupAsync(_groupId, _userId))
                .ReturnsAsync(true);

            // Act
            var result = await _service.GetTaskGroupAsync(_taskId, _userId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(_groupId, result.GroupId);
        }

        #endregion
    }
}