using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Controllers;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Tests.Helpers;
using Xunit;

namespace StudioStudio_Server.Tests.Controllers
{
    public class TestControllerTests
    {
        // ============================
        // 1. Ping DB
        // ============================
        [Fact]
        public async Task Ping_ShouldReturnDatabaseConnectedTrue()
        {
            // Arrange
            var db = DbContextFactory.Create(nameof(Ping_ShouldReturnDatabaseConnectedTrue));
            var controller = new TestController(db);

            // Act
            var result = await controller.Ping();

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(ok.Value);
        }

        // ============================
        // 2. Create User
        // ============================
        [Fact]
        public async Task CreateUser_ShouldCreateUserInMemory()
        {
            // Arrange
            var db = DbContextFactory.Create(nameof(CreateUser_ShouldCreateUserInMemory));
            var controller = new TestController(db);

            // Act
            var result = await controller.CreateUser();

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var user = Assert.IsType<User>(ok.Value);

            Assert.NotEqual(Guid.Empty, user.UserId);
            Assert.Equal(1, db.Users.Count());
        }

        // ============================
        // 3. Create Personal Status
        // ============================
        [Fact]
        public async Task CreatePersonalStatus_ShouldAttachToUser()
        {
            // Arrange
            var db = DbContextFactory.Create(nameof(CreatePersonalStatus_ShouldAttachToUser));
            var controller = new TestController(db);

            var user = new User
            {
                UserId = Guid.NewGuid(),
                Email = "test@mail.com",
                PasswordHash = "hash",
                FirstName = "Test",
                LastName = "User",
                Status = UserStatus.Active
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            // Act
            var result = await controller.CreatePersonalStatus(user.UserId);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var status = Assert.IsType<PersonalTaskStatus>(ok.Value);

            Assert.Equal(user.UserId, status.UserId);
            Assert.Single(db.PersonalTaskStatuses);
        }

        // ============================
        // 4. Create Personal Task
        // ============================
        [Fact]
        public async Task CreatePersonalTask_ShouldHaveNullGroupId()
        {
            // Arrange
            var db = DbContextFactory.Create(nameof(CreatePersonalTask_ShouldHaveNullGroupId));
            var controller = new TestController(db);

            var userId = Guid.NewGuid();
            var statusId = Guid.NewGuid();

            db.Users.Add(new User
            {
                UserId = userId,
                Email = "test@mail.com",
                PasswordHash = "hash",
                FirstName = "Test",
                LastName = "User",
                Status = UserStatus.Active
            });

            db.PersonalTaskStatuses.Add(new PersonalTaskStatus
            {
                StatusId = statusId,
                UserId = userId,
                StatusName = "Todo",
                Position = 1,
                CreatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();

            // Act
            var result = await controller.CreatePersonalTask(userId, statusId);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var task = Assert.IsType<TaskItem>(ok.Value);

            Assert.Null(task.GroupId);
            Assert.Equal(userId, task.OwnerId);
            Assert.Equal(statusId, task.PersonalStatusId);
        }

        // ============================
        // 5. Get Personal Tasks
        // ============================
        [Fact]
        public async Task GetPersonalTasks_ShouldReturnOnlyPersonalTasks()
        {
            // Arrange
            var db = DbContextFactory.Create(nameof(GetPersonalTasks_ShouldReturnOnlyPersonalTasks));
            var controller = new TestController(db);

            var userId = Guid.NewGuid();
            var statusId = Guid.NewGuid();

            db.Users.Add(new User
            {
                UserId = userId,
                Email = "test@mail.com",
                PasswordHash = "hash",
                FirstName = "Test",
                LastName = "User",
                Status = UserStatus.Active
            });

            db.PersonalTaskStatuses.Add(new PersonalTaskStatus
            {
                StatusId = statusId,
                UserId = userId,
                StatusName = "Todo",
                Position = 1,
                CreatedAt = DateTime.UtcNow
            });

            db.Tasks.Add(new TaskItem
            {
                TaskId = Guid.NewGuid(),
                OwnerId = userId,
                GroupId = null,
                PersonalStatusId = statusId,
                Title = "Personal Task",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();

            // Act
            var result = await controller.GetPersonalTasks(userId);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var tasks = Assert.IsAssignableFrom<IEnumerable<TaskItem>>(ok.Value);

            Assert.Single(tasks);
        }
    }
}
