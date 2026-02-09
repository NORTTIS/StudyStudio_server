using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Controllers
{
    [ApiController]
    [Route("api/test")]
    public class TestController : ControllerBase
    {
        private readonly StudioDbContext _db;

        public TestController(StudioDbContext db)
        {
            _db = db;
        }

        // 1. Test DB connection
        [HttpGet("ping")]
        public async Task<IActionResult> Ping()
        {
            var canConnect = await _db.Database.CanConnectAsync();
            return Ok(new
            {
                databaseConnected = canConnect,
                time = DateTime.UtcNow
            });
        }

        // 2. Create test user
        [HttpPost("user")]
        public async Task<IActionResult> CreateUser()
        {
            var user = new User
            {
                UserId = Guid.NewGuid(),
                Email = $"test_{Guid.NewGuid()}@mail.com",
                PasswordHash = "hashed-password",
                FirstName = "Test",
                LastName = "User",
                Status = UserStatus.Active,
                IsAdmin = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return Ok(user);
        }

        // 3. Create personal task status
        [HttpPost("personal-status/{userId}")]
        public async Task<IActionResult> CreatePersonalStatus(Guid userId)
        {
            var status = new PersonalTaskStatus
            {
                StatusId = Guid.NewGuid(),
                UserId = userId,
                StatusName = "To Do",
                Position = 1,
                CreatedAt = DateTime.UtcNow
            };

            _db.PersonalTaskStatuses.Add(status);
            await _db.SaveChangesAsync();

            return Ok(status);
        }

        // 4. Create personal task
        [HttpPost("personal-task/{userId}/{statusId}")]
        public async Task<IActionResult> CreatePersonalTask(
            Guid userId,
            Guid statusId)
        {
            var task = new TaskItem
            {
                TaskId = Guid.NewGuid(),
                OwnerId = userId,
                GroupId = null,
                PersonalStatusId = statusId,
                Title = "My Personal Task",
                Description = "Test personal task",
                Priority = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsPendingDeleted = false
            };

            _db.Tasks.Add(task);
            await _db.SaveChangesAsync();

            return Ok(task);
        }

        // 5. View all personal tasks of user
        [HttpGet("personal-task/{userId}")]
        public async Task<IActionResult> GetPersonalTasks(Guid userId)
        {
            var tasks = await _db.Tasks
                .Include(t => t.PersonalStatus)
                .Where(t => t.OwnerId == userId && t.GroupId == null)
                .OrderBy(t => t.PersonalStatus!.Position)
                .ToListAsync();

            return Ok(tasks);
        }
    }
}
