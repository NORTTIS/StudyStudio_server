using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using StudioStudio_Server.Data;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services.BackgroundServices
{
    /// <summary>
    /// Background job to send daily deadline reminder and overdue notifications for assigned tasks.
    /// Runs once daily at 7:00 AM UTC+7 (0:00 UTC) with Redis deduplication keys (24h TTL).
    /// </summary>
    public class TaskNotificationBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TaskNotificationBackgroundService> _logger;
        private readonly IConnectionMultiplexer _redis;

        // 7:00 AM UTC+7 = 0:00 UTC (Vietnam is UTC+7)
        private static readonly TimeSpan TargetTimeUtc = TimeSpan.Zero;

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
            _logger.LogInformation("Task Notification Background Service started. Runs daily at 07:00 UTC+7 (00:00 UTC)");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var delay = CalculateDelayUntilTargetTimeUtc();
                    _logger.LogInformation(
                        "Next notification run in {Hours:F1} hours ({TargetTime})",
                        delay.TotalHours,
                        DateTime.UtcNow.Add(delay).ToString("yyyy-MM-dd HH:mm:ss 'UTC'"));

                    await Task.Delay(delay, stoppingToken);

                    await ProcessNotificationsAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while processing task notifications");
                    // Wait 1 hour before retrying to avoid tight loop on persistent errors
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }

            _logger.LogInformation("Task Notification Background Service stopped");
        }

        /// <summary>
        /// Calculate delay until next 00:00 UTC (07:00 UTC+7)
        /// </summary>
        private static TimeSpan CalculateDelayUntilTargetTimeUtc()
        {
            var now = DateTime.UtcNow;
            var todayTarget = now.Date + TargetTimeUtc;

            // If we've already passed today's target, wait until tomorrow
            if (now.TimeOfDay >= TargetTimeUtc)
            {
                todayTarget = todayTarget.AddDays(1);
            }

            return todayTarget - now;
        }

        private async Task ProcessNotificationsAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            var redisDb = _redis.GetDatabase();

            var nowUtc = DateTime.UtcNow;
            var todayDate = DateOnly.FromDateTime(nowUtc);

            _logger.LogInformation("Processing daily task notifications for date: {Date}", todayDate);

            // Query all active task assignments
            var assignments = await db.TaskAssignments
                .AsNoTracking()
                .ToListAsync(stoppingToken);

            if (!assignments.Any())
            {
                _logger.LogInformation("No task assignments found");
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
                var dueDateOnly = DateOnly.FromDateTime(dueDate);

                // Reminder: task is due TODAY (7 AM notification for today's deadline)
                if (dueDateOnly == todayDate)
                {
                    var dedupKey = BuildDedupKey(assignment.AssignedTo, task.TaskId, "reminder");
                    var alreadySent = await redisDb.KeyExistsAsync(dedupKey);

                    if (!alreadySent)
                    {
                        await notificationService.NotifyTaskReminderAsync(
                            assignment.AssignedTo,
                            task.TaskId,
                            task.Title,
                            dueDate,
                            hoursUntilDeadline: 0);

                        await redisDb.StringSetAsync(dedupKey, "1", TimeSpan.FromHours(24));
                        reminderCount++;
                    }
                }

                // Overdue: task is past due date and not completed
                if (dueDateOnly < todayDate)
                {
                    var dedupKey = BuildDedupKey(assignment.AssignedTo, task.TaskId, "overdue");
                    var alreadySent = await redisDb.KeyExistsAsync(dedupKey);

                    if (!alreadySent)
                    {
                        var overdueDays = (todayDate.ToDateTime(TimeOnly.MinValue) - dueDate).Days;
                        await notificationService.NotifyTaskOverdueAsync(
                            assignment.AssignedTo,
                            task.TaskId,
                            task.Title,
                            dueDate,
                            overdueDays);

                        await redisDb.StringSetAsync(dedupKey, "1", TimeSpan.FromHours(24));
                        overdueCount++;
                    }
                }
            }

            _logger.LogInformation(
                "Daily task notifications completed. Date: {Date}. Reminders sent: {ReminderCount}, Overdue sent: {OverdueCount}",
                todayDate,
                reminderCount,
                overdueCount);
        }

        private static string BuildDedupKey(Guid userId, Guid taskId, string type)
            => $"notify:{userId}:{taskId}:{type}";
    }
}
