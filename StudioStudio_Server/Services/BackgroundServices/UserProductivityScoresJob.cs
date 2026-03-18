using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Services.BackgroundServices
{
    /// <summary>
    /// Background job to calculate weekly productivity scores
    /// Runs daily at 00:01 to process the previous week's data
    /// </summary>
    public class UserProductivityScoresJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<UserProductivityScoresJob> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

        public UserProductivityScoresJob(
            IServiceProvider serviceProvider,
            ILogger<UserProductivityScoresJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("User Productivity Scores Job started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Calculate next run at 00:01
                    var now = DateTime.UtcNow;
                    var nextRun = now.Date.AddDays(1).AddMinutes(1);
                    var delay = nextRun - now;

                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, stoppingToken);
                    }

                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
                    var analyticsRepository = scope.ServiceProvider
                        .GetRequiredService<IAnalyticsRepository>();

                    _logger.LogInformation("Starting weekly productivity scores calculation...");

                    var weekStart = DateOnly.FromDateTime(now.AddDays(-7));
                    var weekEnd = DateOnly.FromDateTime(now);

                    // Get all users
                    var users = await context.Users
                        .Select(u => u.UserId)
                        .ToListAsync();

                    foreach (var userId in users)
                    {
                        // Get personal tasks
                        var tasksCompleted = await context.Tasks
                            .Where(t => t.OwnerId == userId &&
                                       t.GroupId == null &&
                                       t.CompletedAt.HasValue &&
                                       t.CompletedAt >= DateTime.SpecifyKind(weekStart.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc) &&
                                       t.CompletedAt <= DateTime.SpecifyKind(weekEnd.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc))
                            .CountAsync();

                        var tasksCreated = await context.Tasks
                            .Where(t => t.OwnerId == userId &&
                                       t.GroupId == null &&
                                       t.CreatedAt >= DateTime.SpecifyKind(weekStart.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc) &&
                                       t.CreatedAt <= DateTime.SpecifyKind(weekEnd.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc))
                            .CountAsync();

                        // Calculate on-time completion rate
                        var completedWithDueDate = await context.Tasks
                            .Where(t => t.OwnerId == userId &&
                                       t.GroupId == null &&
                                       t.CompletedAt.HasValue &&
                                       t.DueDate.HasValue &&
                                       t.CompletedAt >= DateTime.SpecifyKind(weekStart.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc) &&
                                       t.CompletedAt <= DateTime.SpecifyKind(weekEnd.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc))
                            .ToListAsync();

                        var onTime = completedWithDueDate.Count(t => t.CompletedAt <= t.DueDate);
                        var totalWithDueDate = completedWithDueDate.Count;
                        var onTimeRate = totalWithDueDate > 0 ? (double)onTime / totalWithDueDate : 0;

                        // Calculate average completion hours
                        var completedWithHours = completedWithDueDate
                            .Where(t => t.CompletedAt.HasValue && t.CreatedAt != null)
                            .ToList();

                        var avgHours = completedWithHours.Any()
                            ? completedWithHours.Average(t =>
                                (t.CompletedAt!.Value - t.CreatedAt).TotalHours)
                            : 0;

                        // Calculate productivity score
                        var productivityScore = CalculateProductivityScore(tasksCompleted, tasksCreated, onTimeRate);

                        var score = new Models.Entities.UserProductivityScores
                        {
                            Id = Guid.NewGuid(),
                            UserId = userId,
                            GroupId = null,
                            WeekStart = weekStart,
                            ProductivityScore = productivityScore,
                            TasksCompleted = tasksCompleted,
                            TasksCreated = tasksCreated,
                            OnTimeCompletionRate = Math.Round(onTimeRate * 100, 2),
                            AverageTaskCompletionHours = Math.Round(avgHours, 2)
                        };

                        await analyticsRepository.UpsertUserProductivityAsync(score);
                    }

                    // Get groups with members for group-specific productivity
                    var groups = await context.Groups
                        .Select(g => g.GroupId)
                        .ToListAsync();

                    foreach (var groupId in groups)
                    {
                        var members = await context.GroupParticipants
                            .Where(p => p.GroupId == groupId)
                            .Select(p => p.UserId)
                            .ToListAsync();

                        foreach (var userId in members)
                        {
                            var tasksCompleted = await context.Tasks
                                .Where(t => t.OwnerId == userId &&
                                           t.GroupId == groupId &&
                                           t.CompletedAt.HasValue &&
                                           t.CompletedAt >= DateTime.SpecifyKind(weekStart.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc) &&
                                           t.CompletedAt <= DateTime.SpecifyKind(weekEnd.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc))
                                .CountAsync();

                            var tasksCreated = await context.Tasks
                                .Where(t => t.OwnerId == userId &&
                                           t.GroupId == groupId &&
                                           t.CreatedAt >= DateTime.SpecifyKind(weekStart.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc) &&
                                           t.CreatedAt <= DateTime.SpecifyKind(weekEnd.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc))
                                .CountAsync();

                            var completedWithDueDate = await context.Tasks
                                .Where(t => t.OwnerId == userId &&
                                           t.GroupId == groupId &&
                                           t.CompletedAt.HasValue &&
                                           t.DueDate.HasValue &&
                                           t.CompletedAt >= DateTime.SpecifyKind(weekStart.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc) &&
                                           t.CompletedAt <= DateTime.SpecifyKind(weekEnd.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc))
                                .ToListAsync();

                            var onTime = completedWithDueDate.Count(t => t.CompletedAt <= t.DueDate);
                            var totalWithDueDate = completedWithDueDate.Count;
                            var onTimeRate = totalWithDueDate > 0 ? (double)onTime / totalWithDueDate : 0;

                            var productivityScore = CalculateProductivityScore(tasksCompleted, tasksCreated, onTimeRate);

                            var score = new Models.Entities.UserProductivityScores
                            {
                                Id = Guid.NewGuid(),
                                UserId = userId,
                                GroupId = groupId,
                                WeekStart = weekStart,
                                ProductivityScore = productivityScore,
                                TasksCompleted = tasksCompleted,
                                TasksCreated = tasksCreated,
                                OnTimeCompletionRate = Math.Round(onTimeRate * 100, 2),
                                AverageTaskCompletionHours = 0
                            };

                            await analyticsRepository.UpsertUserProductivityAsync(score);
                        }
                    }

                    _logger.LogInformation("Weekly productivity scores calculation completed. Processed {UserCount} users and {GroupCount} groups",
                        users.Count, groups.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during weekly productivity scores calculation");
                }
            }

            _logger.LogInformation("User Productivity Scores Job stopped");
        }

        private double CalculateProductivityScore(int tasksCompleted, int tasksCreated, double onTimeRate)
        {
            // Weighted scoring: Tasks completed (40%), Tasks created (20%), On-time rate (40%)
            var taskScore = Math.Min(tasksCompleted * 5, 40);
            var creationScore = Math.Min(tasksCreated * 2, 20);
            var onTimeScore = onTimeRate * 40;

            return Math.Round(taskScore + creationScore + onTimeScore, 2);
        }
    }
}
