using Microsoft.AspNetCore.Http;
using Moq;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services;
using Xunit;

namespace StudioStudio_Server.Tests.Services
{
    public class HomeServiceTests
    {
        private readonly Mock<ITaskAssignmentRepository> _assignmentRepoMock;
        private readonly Mock<ITaskRepository> _taskRepoMock;
        private readonly Mock<IGroupRepository> _groupRepoMock;
        private readonly Mock<IGroupTaskStatusRepository> _groupTaskStatusRepoMock;
        private readonly Mock<IPersonalTaskStatusRepository> _personalTaskStatusRepoMock;
        private readonly Mock<IUserRepository> _userRepoMock;
        private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
        private HomeService _service = null!;

        private readonly Guid _userId = Guid.NewGuid();

        public HomeServiceTests()
        {
            _assignmentRepoMock = new Mock<ITaskAssignmentRepository>();
            _taskRepoMock = new Mock<ITaskRepository>();
            _groupRepoMock = new Mock<IGroupRepository>();
            _groupTaskStatusRepoMock = new Mock<IGroupTaskStatusRepository>();
            _personalTaskStatusRepoMock = new Mock<IPersonalTaskStatusRepository>();
            _userRepoMock = new Mock<IUserRepository>();
            _httpContextAccessorMock = new Mock<IHttpContextAccessor>();

            _service = new HomeService(
                _assignmentRepoMock.Object,
                _taskRepoMock.Object,
                _groupRepoMock.Object,
                _groupTaskStatusRepoMock.Object,
                _personalTaskStatusRepoMock.Object,
                _userRepoMock.Object,
                _httpContextAccessorMock.Object);
        }

        #region GetPersonalTaskBoardAsync

        [Fact]
        public async Task GetPersonalTaskBoardAsync_UserNotFound_ThrowsNotFound()
        {
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync((User?)null);

            var ex = await Assert.ThrowsAsync<AppException>(() => _service.GetPersonalTaskBoardAsync(_userId));
            Assert.Equal(ErrorCodes.UserNotFound, ex.Code);
        }

        [Fact]
        public async Task GetPersonalTaskBoardAsync_NoStatuses_ReturnsEmpty()
        {
            var user = new User { UserId = _userId, FirstName = "John", LastName = "Doe" };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);
            _personalTaskStatusRepoMock.Setup(x => x.GetAllByUserIdAsync(_userId))
                .ReturnsAsync(new List<PersonalTaskStatus>());
            _taskRepoMock.Setup(x => x.GetPersonalListTasksByListStatusId(It.IsAny<List<Guid>>()))
                .ReturnsAsync(new Dictionary<Guid, List<TaskItem>>());

            var result = await _service.GetPersonalTaskBoardAsync(_userId);

            Assert.Empty(result.PersonalTaskStatuses);
        }

        [Fact]
        public async Task GetPersonalTaskBoardAsync_WithStatuses_ReturnsBoard()
        {
            var user = new User { UserId = _userId, FirstName = "John", LastName = "Doe" };
            var status = new PersonalTaskStatus { StatusId = Guid.NewGuid(), UserId = _userId, StatusName = "To Do", Position = 1000 };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);
            _personalTaskStatusRepoMock.Setup(x => x.GetAllByUserIdAsync(_userId))
                .ReturnsAsync(new List<PersonalTaskStatus> { status });
            _taskRepoMock.Setup(x => x.GetPersonalListTasksByListStatusId(It.IsAny<List<Guid>>()))
                .ReturnsAsync(new Dictionary<Guid, List<TaskItem>>
                {
                    { status.StatusId, new List<TaskItem> { new TaskItem { TaskId = Guid.NewGuid(), Title = "Task 1", Priority = TaskPriority.Low, Severity = TaskSeverity.Minor, Position = 1000, CreatedAt = DateTime.UtcNow, OwnerId = _userId } } }
                });

            var result = await _service.GetPersonalTaskBoardAsync(_userId);

            Assert.Single(result.PersonalTaskStatuses);
            Assert.Equal("To Do", result.PersonalTaskStatuses[0].StatusName);
            Assert.Single(result.PersonalTaskStatuses[0].TaskList);
            Assert.Equal("Task 1", result.PersonalTaskStatuses[0].TaskList[0].TaskTitle);
        }

        #endregion

        #region GetHomeSummaryAsync

        [Fact]
        public async Task GetHomeSummaryAsync_UserNotFound_ThrowsNotFound()
        {
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync((User?)null);

            var ex = await Assert.ThrowsAsync<AppException>(() => _service.GetHomeSummaryAsync(_userId));
            Assert.Equal(ErrorCodes.UserNotFound, ex.Code);
        }

        [Fact]
        public async Task GetHomeSummaryAsync_WithTasks_ReturnsSummary()
        {
            var user = new User { UserId = _userId };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);
            _taskRepoMock.Setup(x => x.GetPersonalTasksByOwnerAsync(_userId))
                .ReturnsAsync(new List<TaskItem>
                {
                    new() { TaskId = Guid.NewGuid(), Progress = 0, DueDate = DateTime.UtcNow.AddDays(1) },
                    new() { TaskId = Guid.NewGuid(), Progress = 100 },
                    new() { TaskId = Guid.NewGuid(), Progress = 50, DueDate = DateTime.UtcNow.AddDays(-1) }
                });
            _taskRepoMock.Setup(x => x.GetAssignedGroupTasksByUserAsync(_userId))
                .ReturnsAsync(new List<TaskItem>());
            _groupRepoMock.Setup(x => x.GetUserGroupsAsync(_userId))
                .ReturnsAsync(new List<Group> { new(), new() });

            var result = await _service.GetHomeSummaryAsync(_userId);

            Assert.Equal(2, result.RemainingTaskCount);
            Assert.Equal(1, result.OverdueTaskCount);
            Assert.Equal(1, result.CompletedTaskCount);
            Assert.Equal(2, result.TotalJoinedGroupCount);
        }

        #endregion

        #region GetHomeTaskListAsync

        [Fact]
        public async Task GetHomeTaskListAsync_UserNotFound_ThrowsNotFound()
        {
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync((User?)null);

            var ex = await Assert.ThrowsAsync<AppException>(() => _service.GetHomeTaskListAsync(_userId, 1, 10));
            Assert.Equal(ErrorCodes.UserNotFound, ex.Code);
        }

        [Fact]
        public async Task GetHomeTaskListAsync_ValidUser_ReturnsPaginatedTasks()
        {
            var user = new User { UserId = _userId };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);
            _taskRepoMock.Setup(x => x.GetAssignedGroupTasksWithPaginationAsync(_userId, 1, 10, null, null, true))
                .ReturnsAsync((new List<TaskItem> { new TaskItem { TaskId = Guid.NewGuid(), Title = "Group Task", Priority = TaskPriority.High, Severity = TaskSeverity.Critical, Progress = 0 } }, 1));
            _groupRepoMock.Setup(x => x.GetUserGroupsAsync(_userId))
                .ReturnsAsync(new List<Group> { new Group { GroupId = Guid.NewGuid(), GroupName = "Team Alpha" } });

            var result = await _service.GetHomeTaskListAsync(_userId, 1, 10);

            Assert.Single(result.Items);
            Assert.Equal("Group Task", result.Items[0].TaskTitle);
            Assert.Equal("Group", result.Items[0].SourceType);
            Assert.Equal(1, result.TotalCount);
            Assert.Equal(1, result.Page);
        }

        [Fact]
        public async Task GetHomeTaskListAsync_NegativePage_UsesDefault()
        {
            var user = new User { UserId = _userId };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);
            _taskRepoMock.Setup(x => x.GetAssignedGroupTasksWithPaginationAsync(_userId, 1, 10, null, null, true))
                .ReturnsAsync((new List<TaskItem>(), 0));
            _groupRepoMock.Setup(x => x.GetUserGroupsAsync(_userId))
                .ReturnsAsync(new List<Group>());

            var result = await _service.GetHomeTaskListAsync(_userId, -5, -1);

            Assert.Equal(1, result.Page);
            Assert.Equal(10, result.PageSize);
        }

        #endregion

        #region CreateNewPersonalTaskStatus

        [Fact]
        public async Task CreateNewPersonalTaskStatus_UserNotFound_ThrowsNotFound()
        {
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync((User?)null);

            var ex = await Assert.ThrowsAsync<AppException>(() => _service.CreateNewPersonalTaskStatus(_userId, new PersonalTaskStatusRequest()));
            Assert.Equal(ErrorCodes.UserNotFound, ex.Code);
        }

        [Fact]
        public async Task CreateNewPersonalTaskStatus_NameExists_ThrowsBadRequest()
        {
            var user = new User { UserId = _userId };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);
            _personalTaskStatusRepoMock.Setup(x => x.GetAllByUserIdAsync(_userId))
                .ReturnsAsync(new List<PersonalTaskStatus>());
            _personalTaskStatusRepoMock.Setup(x => x.IsNameExist(It.IsAny<PersonalTaskStatus>()))
                .ReturnsAsync(true);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.CreateNewPersonalTaskStatus(_userId, new PersonalTaskStatusRequest { StatusName = "To Do" }));
            Assert.Equal(ErrorCodes.StatusNameExist, ex.Code);
        }

        [Fact]
        public async Task CreateNewPersonalTaskStatus_ValidRequest_CreatesStatus()
        {
            var user = new User { UserId = _userId };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);
            _personalTaskStatusRepoMock.Setup(x => x.GetAllByUserIdAsync(_userId))
                .ReturnsAsync(new List<PersonalTaskStatus>());
            _personalTaskStatusRepoMock.Setup(x => x.IsNameExist(It.IsAny<PersonalTaskStatus>()))
                .ReturnsAsync(false);

            var result = await _service.CreateNewPersonalTaskStatus(_userId, new PersonalTaskStatusRequest { StatusName = "In Progress" });

            Assert.Equal("In Progress", result.StatusName);
            Assert.Equal(1000, result.Position);
            _personalTaskStatusRepoMock.Verify(x => x.AddAsync(It.IsAny<PersonalTaskStatus>()), Times.Once);
        }

        [Fact]
        public async Task CreateNewPersonalTaskStatus_WithExisting_CalculatesPosition()
        {
            var user = new User { UserId = _userId };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);
            _personalTaskStatusRepoMock.Setup(x => x.GetAllByUserIdAsync(_userId))
                .ReturnsAsync(new List<PersonalTaskStatus>
                {
                    new() { StatusId = Guid.NewGuid(), UserId = _userId, StatusName = "Done", Position = 2000 }
                });
            _personalTaskStatusRepoMock.Setup(x => x.IsNameExist(It.IsAny<PersonalTaskStatus>()))
                .ReturnsAsync(false);

            var result = await _service.CreateNewPersonalTaskStatus(_userId, new PersonalTaskStatusRequest { StatusName = "New" });

            Assert.Equal(3000, result.Position);
        }

        #endregion

        #region DeletePersonalTaskStatus

        [Fact]
        public async Task DeletePersonalTaskStatus_UserNotFound_ThrowsNotFound()
        {
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync((User?)null);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.DeletePersonalTaskStatus(_userId, Guid.NewGuid()));
            Assert.Equal(ErrorCodes.UserNotFound, ex.Code);
        }

        [Fact]
        public async Task DeletePersonalTaskStatus_NotOwner_ThrowsNotFound()
        {
            var user = new User { UserId = _userId };
            var status = new PersonalTaskStatus { StatusId = Guid.NewGuid(), UserId = Guid.NewGuid() };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);
            _personalTaskStatusRepoMock.Setup(x => x.GetDetailAsync(status.StatusId))
                .ReturnsAsync(status);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.DeletePersonalTaskStatus(_userId, status.StatusId));
            Assert.Equal(ErrorCodes.StatusNotFound, ex.Code);
        }

        [Fact]
        public async Task DeletePersonalTaskStatus_HasTasks_ThrowsBadRequest()
        {
            var user = new User { UserId = _userId };
            var status = new PersonalTaskStatus { StatusId = Guid.NewGuid(), UserId = _userId };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);
            _personalTaskStatusRepoMock.Setup(x => x.GetDetailAsync(status.StatusId))
                .ReturnsAsync(status);
            _taskRepoMock.Setup(x => x.GetAllPersonalTasksByStatusIdAsync(status.StatusId))
                .ReturnsAsync(new List<TaskItem> { new() });

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.DeletePersonalTaskStatus(_userId, status.StatusId));
            Assert.Equal(ErrorCodes.GroupDeleteTaskStatusFailed, ex.Code);
        }

        [Fact]
        public async Task DeletePersonalTaskStatus_Valid_DeletesStatus()
        {
            var user = new User { UserId = _userId };
            var status = new PersonalTaskStatus { StatusId = Guid.NewGuid(), UserId = _userId };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);
            _personalTaskStatusRepoMock.Setup(x => x.GetDetailAsync(status.StatusId))
                .ReturnsAsync(status);
            _taskRepoMock.Setup(x => x.GetAllPersonalTasksByStatusIdAsync(status.StatusId))
                .ReturnsAsync(new List<TaskItem>());

            await _service.DeletePersonalTaskStatus(_userId, status.StatusId);

            _personalTaskStatusRepoMock.Verify(x => x.DeletePersonalStatusAsync(status), Times.Once);
        }

        #endregion

        #region UpdatePersonalTaskStatus

        [Fact]
        public async Task UpdatePersonalTaskStatus_UserNotFound_ThrowsNotFound()
        {
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync((User?)null);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdatePersonalTaskStatus(_userId, Guid.NewGuid(), new PersonalTaskStatusRequest()));
            Assert.Equal(ErrorCodes.UserNotFound, ex.Code);
        }

        [Fact]
        public async Task UpdatePersonalTaskStatus_NotOwner_ThrowsNotFound()
        {
            var user = new User { UserId = _userId };
            var status = new PersonalTaskStatus { StatusId = Guid.NewGuid(), UserId = Guid.NewGuid(), StatusName = "Old" };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);
            _personalTaskStatusRepoMock.Setup(x => x.GetDetailAsync(status.StatusId))
                .ReturnsAsync(status);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdatePersonalTaskStatus(_userId, status.StatusId, new PersonalTaskStatusRequest { StatusName = "New" }));
            Assert.Equal(ErrorCodes.StatusNotFound, ex.Code);
        }

        [Fact]
        public async Task UpdatePersonalTaskStatus_NameExists_ThrowsBadRequest()
        {
            var user = new User { UserId = _userId };
            var status = new PersonalTaskStatus { StatusId = Guid.NewGuid(), UserId = _userId, StatusName = "Old" };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);
            _personalTaskStatusRepoMock.Setup(x => x.GetDetailAsync(status.StatusId))
                .ReturnsAsync(status);
            _personalTaskStatusRepoMock.Setup(x => x.IsNameExist(status))
                .ReturnsAsync(true);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdatePersonalTaskStatus(_userId, status.StatusId, new PersonalTaskStatusRequest { StatusName = "Duplicate" }));
            Assert.Equal(ErrorCodes.StatusNameExist, ex.Code);
        }

        [Fact]
        public async Task UpdatePersonalTaskStatus_Valid_UpdatesStatus()
        {
            var user = new User { UserId = _userId };
            var status = new PersonalTaskStatus { StatusId = Guid.NewGuid(), UserId = _userId, StatusName = "Old" };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);
            _personalTaskStatusRepoMock.Setup(x => x.GetDetailAsync(status.StatusId))
                .ReturnsAsync(status);
            _personalTaskStatusRepoMock.Setup(x => x.IsNameExist(status))
                .ReturnsAsync(false);

            await _service.UpdatePersonalTaskStatus(_userId, status.StatusId, new PersonalTaskStatusRequest { StatusName = "New Name" });

            Assert.Equal("New Name", status.StatusName);
            _personalTaskStatusRepoMock.Verify(x => x.UpdatePersonalStatusAsync(status), Times.Once);
        }

        #endregion

        #region ReorderPersonalTaskStatus

        [Fact]
        public async Task ReorderPersonalTaskStatus_UserNotFound_ThrowsNotFound()
        {
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync((User?)null);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ReorderPersonalTaskStatus(_userId, new ReorderPersonalTaskStatusRequest()));
            Assert.Equal(ErrorCodes.UserNotFound, ex.Code);
        }

        [Fact]
        public async Task ReorderPersonalTaskStatus_NotOwner_ThrowsNotFound()
        {
            var user = new User { UserId = _userId };
            var statusId = Guid.NewGuid();
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);
            _personalTaskStatusRepoMock.Setup(x => x.GetDetailAsync(statusId))
                .ReturnsAsync(new PersonalTaskStatus { StatusId = statusId, UserId = Guid.NewGuid() });

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ReorderPersonalTaskStatus(_userId, new ReorderPersonalTaskStatusRequest { StatusId = statusId }));
            Assert.Equal(ErrorCodes.StatusNotFound, ex.Code);
        }

        [Fact]
        public async Task ReorderPersonalTaskStatus_Valid_CallsReorder()
        {
            var user = new User { UserId = _userId };
            var statusId = Guid.NewGuid();
            var prevId = Guid.NewGuid();
            var nextId = Guid.NewGuid();
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);
            _personalTaskStatusRepoMock.Setup(x => x.GetDetailAsync(statusId))
                .ReturnsAsync(new PersonalTaskStatus { StatusId = statusId, UserId = _userId });

            await _service.ReorderPersonalTaskStatus(_userId, new ReorderPersonalTaskStatusRequest
            {
                StatusId = statusId,
                PrevStatusId = prevId,
                NextStatusId = nextId
            });

            _personalTaskStatusRepoMock.Verify(x => x.ReorderStatusAsync(statusId, prevId, nextId, _userId), Times.Once);
        }

        #endregion
    }
}