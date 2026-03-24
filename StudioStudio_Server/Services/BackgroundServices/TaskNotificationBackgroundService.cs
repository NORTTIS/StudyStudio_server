using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using StudioStudio_Server.Data;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services.BackgroundServices
{
    /// <summary>
    /// Background job to send deadline reminder and overdue notifications for assigned tasks.
    /// Runs every hour with Redis deduplication keys (24h TTL) to avoid spam.
    /// </summary>
    public class TaskNotificationBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TaskNotificationBackgroundService> _logger;
        private readonly IConnectionMultiplexer _redis;

        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
        private static readonly TimeSpan DedupTtl = TimeSpan.FromHours(24);

        public TaskNotificationBackgroundService(
            IServiceProvider serviceProvider,
            IConnectionMultiplexer redis,
            ILogger<TaskNotificationBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _redis = redis;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Task Notification Background Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessNotificationsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while processing task notifications");
                }

                await Task.Delay(Interval, stoppingToken);
            }

            _logger.LogInformation("Task Notification Background Service stopped");
        }

        private async Task ProcessNotificationsAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var redisDb = _redis.GetDatabase();

            var now = DateTime.UtcNow;
            var upcomingThreshold = now.AddHours(24);

            // Query assigned tasks with due date in next 24h and not completed
            var assignments = await db.TaskAssignments
                .AsNoTracking()
                .ToListAsync(stoppingToken);

            if (!assignments.Any())
            {
                return;
            }

            var taskIds = assignments.Select(a => a.TaskId).Distinct().ToList();
            var tasks = await db.Tasks
                .Where(t => taskIds.Contains(t.TaskId) && t.DueDate.HasValue && t.Progress < 100 && !t.IsPendingDeleted)
                .AsNoTracking()
                .ToListAsync(stoppingToken);

            var taskDict = tasks.ToDictionary(t => t.TaskId, t => t);

            int reminderCount = 0;
            int overdueCount = 0;

            foreach (var assignment in assignments)
            {
                if (!taskDict.TryGetValue(assignment.TaskId, out var task) || !task.DueDate.HasValue)
                    continue;

                var dueDate = task.DueDate.Value;

                // Reminder: due within 24h
                if (dueDate > now && dueDate <= upcomingThreshold)
                {
                    var reminderKey = BuildDedupKey(assignment.AssignedTo, task.TaskId, "reminder");
                    var alreadySent = await redisDb.KeyExistsAsync(reminderKey);

                    if (!alreadySent)
                    {
                        var hoursUntilDeadline = Math.Max(1, (int)Math.Ceiling((dueDate - now).TotalHours));
                        await notificationService.NotifyTaskReminderAsync(
                            assignment.AssignedTo,
                            task.TaskId,
                            task.Title,
                            dueDate,
                            hoursUntilDeadline);

                        await redisDb.StringSetAsync(reminderKey, "1", DedupTtl);
                        reminderCount++;
                    }
                }

                // Overdue: due passed and not completed
                if (dueDate < now)
                {
                    var overdueKey = BuildDedupKey(assignment.AssignedTo, task.TaskId, "overdue");
                    var alreadySent = await redisDb.KeyExistsAsync(overdueKey);

                    if (!alreadySent)
                    {
                        var overdueDays = Math.Max(1, (now.Date - dueDate.Date).Days);
                        await notificationService.NotifyTaskOverdueAsync(
                            assignment.AssignedTo,
                            task.TaskId,
                            task.Title,
                            dueDate,
                            overdueDays);

                        await redisDb.StringSetAsync(overdueKey, "1", DedupTtl);
                        overdueCount++;
                    }
                }
            }

            _logger.LogInformation(
                "Task notifications processed. Reminders sent: {ReminderCount}, Overdue sent: {OverdueCount}",
                reminderCount,
                overdueCount);
        }

        private static string BuildDedupKey(Guid userId, Guid taskId, string type)
            => $"notify:{userId}:{taskId}:{type}";
    }
}
