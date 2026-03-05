using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Controllers
{
    /// <summary>
    /// Controller cho testing và development
    /// Route: /api/test
    /// WARNING: Chỉ dùng cho môi trường Development
    /// TODO: Disable trong Production hoặc add authentication
    /// </summary>
    [ApiController]
    [Route("api/test")]
    public class TestController : ControllerBase
    {
        private readonly StudioDbContext _db;
        private readonly ILogger<TestController> _logger;

        public TestController(StudioDbContext db, ILogger<TestController> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// [TEST] GET /api/test/ping
        /// Test database connection
        /// Return: Connection status + current UTC time
        /// </summary>
        [HttpGet("ping")]
        public async Task<IActionResult> Ping()
        {
            var canConnect = await _db.Database.CanConnectAsync();

            _logger.LogInformation("Database connection test: {Status}", canConnect);

            return Ok(new
            {
                databaseConnected = canConnect,
                time = DateTime.UtcNow,
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            });
        }

        /// <summary>
        /// [TEST] POST /api/test/user
        /// Tạo test user
        /// Auto-generate: Email, UserId
        /// </summary>
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
                Language = "vi",
                EmailNotificationEnabled = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Test user created: {UserId}, {Email}", user.UserId, user.Email);

            return Ok(user);
        }

        /// <summary>
        /// [TEST] POST /api/test/personal-status/{userId}
        /// Tạo personal task status cho user
        /// </summary>
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

            _logger.LogInformation(
                "Personal status created: StatusId={StatusId}, UserId={UserId}",
                status.StatusId, userId);

            return Ok(status);
        }

        /// <summary>
        /// [TEST] POST /api/test/personal-task/{userId}/{statusId}
        /// Tạo personal task cho user
        /// </summary>
        [HttpPost("personal-task/{userId}/{statusId}")]
        public async Task<IActionResult> CreatePersonalTask(Guid userId, Guid statusId)
        {
            var task = new TaskItem
            {
                TaskId = Guid.NewGuid(),
                OwnerId = userId,
                GroupId = null,
                PersonalStatusId = statusId,
                Title = "My Personal Task",
                Description = "Test personal task",
                Priority = TaskPriority.Low,
                Severity = TaskSeverity.Minor,
                Position = 1000,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsPendingDeleted = false
            };

            _db.Tasks.Add(task);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Personal task created: TaskId={TaskId}, UserId={UserId}",
                task.TaskId, userId);

            return Ok(task);
        }

        /// <summary>
        /// [TEST] GET /api/test/personal-task/{userId}
        /// Lấy tất cả personal tasks của user
        /// Điều kiện: OwnerId = {userId} AND GroupId = null
        /// Sắp xếp: PersonalStatus.Position ASC
        /// Include: PersonalStatus
        /// </summary>
        [HttpGet("personal-task/{userId}")]
        public async Task<IActionResult> GetPersonalTasks(Guid userId)
        {
            var tasks = await _db.Tasks
                .Include(t => t.PersonalStatus)
                .Where(t => t.OwnerId == userId && t.GroupId == null)
                .OrderBy(t => t.PersonalStatus!.Position)
                .AsNoTracking()
                .ToListAsync();

            _logger.LogInformation(
                "Retrieved {Count} personal tasks for user {UserId}",
                tasks.Count, userId);

            return Ok(tasks);
        }
    }
}
