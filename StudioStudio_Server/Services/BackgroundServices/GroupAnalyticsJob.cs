using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Services.BackgroundServices
{
    /// <summary>
    /// Background job to aggregate group daily analytics metrics
    /// Runs every 10 minutes to process activity from the last interval
    /// </summary>
    public class GroupAnalyticsJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<GroupAnalyticsJob> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

        public GroupAnalyticsJob(
            IServiceProvider serviceProvider,
            ILogger<GroupAnalyticsJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Group Analytics Job started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(Interval, stoppingToken);

                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
                    var analyticsRepository = scope.ServiceProvider
                        .GetRequiredService<IAnalyticsRepository>();

                    _logger.LogInformation("Starting group analytics aggregation...");

                    var to = DateTime.UtcNow;
                    var from = to.AddMinutes(-10);
                    var today = DateOnly.FromDateTime(DateTime.UtcNow);

                    // Get all active, non-archived groups and studios
                    var groups = await context.Groups
                        .Where(g => g.IsActive && !g.IsArchived && (g.StudioId == null || !g.Studio!.IsArchived))
                        .Select(g => g.GroupId)
                        .ToListAsync();

                    foreach (var groupId in groups)
                    {
                        // Get group metrics
                        var totalTasks = await context.Tasks
                            .Where(t => t.GroupId == groupId)
                            .CountAsync();

                        var completedTasks = await context.Tasks
                            .Where(t => t.GroupId == groupId && t.Progress == 100)
                            .CountAsync();

                        var overdueTasks = await context.Tasks
                            .Where(t => t.GroupId == groupId && t.DueDate < to && t.Progress < 100)
                            .CountAsync();

                        // Get active members (users who performed activities in the period)
                        var activeMembers = await context.ActivityLogs
                            .Where(a => a.GroupId == groupId && a.CreatedAt >= from && a.CreatedAt <= to)
                            .Select(a => a.UserId)
                            .Distinct()
                            .CountAsync();

                        // Get messages count
                        var messagesCount = await context.GroupMessages
                            .Where(m => m.GroupId == groupId && m.CreatedAt >= from && m.CreatedAt <= to)
                            .CountAsync();

                        // Get comments count
                        var commentsCount = await context.TaskComments
                            .Where(c => c.Task.GroupId == groupId && c.CreatedAt >= from && c.CreatedAt <= to)
                            .CountAsync();

                        var completionRate = totalTasks > 0
                            ? Math.Round((double)completedTasks / totalTasks * 100, 2)
                            : 0;

                        var analytics = new Models.Entities.GroupAnalytics
                        {
                            Id = Guid.NewGuid(),
                            GroupId = groupId,
                            Date = today,
                            TotalTasks = totalTasks,
                            CompletedTasks = completedTasks,
                            OverdueTasks = overdueTasks,
                            ActiveMembers = activeMembers,
                            MessagesCount = messagesCount,
                            CommentsCount = commentsCount,
                            CompletionRate = completionRate
                        };

                        await analyticsRepository.UpsertGroupAnalyticsAsync(analytics);
                    }

                    _logger.LogInformation("Group analytics aggregation completed. Processed {Count} groups", groups.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during group analytics aggregation");
                }
            }

            _logger.LogInformation("Group Analytics Job stopped");
        }
    }
}
