using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Services.BackgroundServices
{
    /// <summary>
    /// Background job to aggregate studio daily analytics metrics
    /// Runs daily at 00:30 to process the previous day's data
    /// </summary>
    public class StudioAnalyticsJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<StudioAnalyticsJob> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

        public StudioAnalyticsJob(
            IServiceProvider serviceProvider,
            ILogger<StudioAnalyticsJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Studio Analytics Job started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Calculate next run at 00:30
                    var now = DateTime.UtcNow;
                    var nextRun = now.Date.AddDays(1).AddHours(0).AddMinutes(30);
                    var delay = nextRun - now;

                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, stoppingToken);
                    }

                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
                    var analyticsRepository = scope.ServiceProvider
                        .GetRequiredService<IAnalyticsRepository>();

                    _logger.LogInformation("Starting studio analytics aggregation...");

                    var today = DateOnly.FromDateTime(DateTime.UtcNow);
                    var yesterday = today.AddDays(-1);

                    // Get all studios
                    var studios = await context.Studios
                        .Select(s => s.StudioId)
                        .ToListAsync();

                    foreach (var studioId in studios)
                    {
                        // Get total groups
                        var totalGroups = await context.Groups
                            .Where(g => g.StudioId == studioId)
                            .CountAsync();

                        // Get active groups (groups with activity in the last 7 days)
                        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
                        var activeGroupIds = await context.ActivityLogs
                            .Where(a => a.StudioId == studioId && a.CreatedAt >= sevenDaysAgo)
                            .Select(a => a.GroupId)
                            .Distinct()
                            .ToListAsync();

                        var activeGroups = await context.Groups
                            .Where(g => g.StudioId == studioId && activeGroupIds.Contains(g.GroupId))
                            .CountAsync();

                        // Get total members
                        var totalMembers = await context.StudioParticipants
                            .Where(p => p.StudioId == studioId)
                            .CountAsync();

                        // Get active members (members with activity in the last 7 days)
                        var activeMembers = await context.ActivityLogs
                            .Where(a => a.StudioId == studioId && a.CreatedAt >= sevenDaysAgo)
                            .Select(a => a.UserId)
                            .Distinct()
                            .CountAsync();

                        // Get tasks completed
                        var groupIds = await context.Groups
                            .Where(g => g.StudioId == studioId)
                            .Select(g => g.GroupId)
                            .ToListAsync();

                        var tasksCompleted = await context.Tasks
                            .Where(t => groupIds.Contains(t.GroupId!.Value) && t.Progress == 100)
                            .CountAsync();

                        // Calculate overall completion rate
                        var totalTasks = await context.Tasks
                            .Where(t => groupIds.Contains(t.GroupId!.Value))
                            .CountAsync();

                        var overallCompletionRate = totalTasks > 0
                            ? Math.Round((double)tasksCompleted / totalTasks * 100, 2)
                            : 0;

                        // Calculate engagement score (0-100)
                        var engagementScore = CalculateEngagementScore(
                            activeGroups,
                            totalGroups,
                            activeMembers,
                            totalMembers,
                            overallCompletionRate);

                        var analytics = new Models.Entities.StudioAnalytics
                        {
                            Id = Guid.NewGuid(),
                            StudioId = studioId,
                            Date = today,
                            TotalGroups = totalGroups,
                            ActiveGroups = activeGroups,
                            TotalMembers = totalMembers,
                            ActiveMembers = activeMembers,
                            TasksCompleted = tasksCompleted,
                            OverallCompletionRate = overallCompletionRate,
                            EngagementScore = engagementScore
                        };

                        await analyticsRepository.UpsertStudioAnalyticsAsync(analytics);
                    }

                    _logger.LogInformation("Studio analytics aggregation completed. Processed {Count} studios", studios.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during studio analytics aggregation");
                }
            }

            _logger.LogInformation("Studio Analytics Job stopped");
        }

        private double CalculateEngagementScore(int activeGroups, int totalGroups, int activeMembers, int totalMembers, double completionRate)
        {
            // Weighted scoring: Group activity (30%), Member activity (30%), Completion rate (40%)
            var groupActivity = totalGroups > 0 ? (double)activeGroups / totalGroups * 30 : 0;
            var memberActivity = totalMembers > 0 ? (double)activeMembers / totalMembers * 30 : 0;
            var completionScore = completionRate * 0.4;

            return Math.Round(groupActivity + memberActivity + completionScore, 2);
        }
    }
}
