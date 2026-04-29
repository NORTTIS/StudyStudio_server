using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;

namespace StudioStudio_Server.Services.BackgroundServices
{
    /// <summary>
    /// Background job to aggregate group daily analytics metrics
    /// Runs every 10 minutes to process activity from the last interval
    /// </summary>
    public class GroupAnalyticsJob(
        IServiceProvider serviceProvider,
        ILogger<GroupAnalyticsJob> logger) : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

        private sealed record GroupTaskSnapshot(
            int TotalTasks,
            int CompletedTasks,
            int OverdueTasks);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Group Analytics Job started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(Interval, stoppingToken);

                    using var scope = serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<StudioDbContext>();

                    logger.LogInformation("Starting group analytics aggregation...");

                    var to = DateTime.UtcNow;
                    var from = to.AddMinutes(-10);
                    var today = DateOnly.FromDateTime(to);

                    // Get all active, non-archived groups and studios
                    var groups = await context.Groups
                        .Where(g => g.IsActive && !g.IsArchived && (g.StudioId == null || !g.Studio!.IsArchived))
                        .Select(g => g.GroupId)
                        .ToListAsync(stoppingToken);

                    if (groups.Count == 0)
                    {
                        logger.LogInformation("No active groups found for group analytics aggregation");
                        continue;
                    }

                    var taskMetrics = await context.Tasks
                        .AsNoTracking()
                        .Where(t => t.GroupId.HasValue && groups.Contains(t.GroupId.Value))
                        .GroupBy(t => t.GroupId!.Value)
                        .Select(g => new
                        {
                            GroupId = g.Key,
                            TotalTasks = g.Count(),
                            CompletedTasks = g.Count(t => t.Progress == 100),
                            OverdueTasks = g.Count(t => t.DueDate.HasValue && t.DueDate.Value < to && t.Progress < 100)
                        })
                        .ToDictionaryAsync(x => x.GroupId, x => new GroupTaskSnapshot(
                            x.TotalTasks,
                            x.CompletedTasks,
                            x.OverdueTasks), stoppingToken);

                    var activeMembersByGroup = await context.ActivityLogs
                        .AsNoTracking()
                        .Where(a => a.GroupId.HasValue && groups.Contains(a.GroupId.Value) &&
                                    a.CreatedAt >= from && a.CreatedAt <= to)
                        .GroupBy(a => a.GroupId!.Value)
                        .Select(g => new
                        {
                            GroupId = g.Key,
                            Count = g.Select(x => x.UserId).Distinct().Count()
                        })
                        .ToDictionaryAsync(x => x.GroupId, x => x.Count, stoppingToken);

                    var messagesByGroup = await context.GroupMessages
                        .AsNoTracking()
                        .Where(m => groups.Contains(m.GroupId) && m.CreatedAt >= from && m.CreatedAt <= to)
                        .GroupBy(m => m.GroupId)
                        .Select(g => new
                        {
                            GroupId = g.Key,
                            Count = g.Count()
                        })
                        .ToDictionaryAsync(x => x.GroupId, x => x.Count, stoppingToken);

                    var commentsByGroup = await context.TaskComments
                        .AsNoTracking()
                        .Where(c => c.CreatedAt >= from && c.CreatedAt <= to)
                        .Join(
                            context.Tasks.AsNoTracking(),
                            comment => comment.TaskId,
                            task => task.TaskId,
                            (comment, task) => new { comment, task })
                        .Where(x => x.task.GroupId.HasValue && groups.Contains(x.task.GroupId.Value))
                        .GroupBy(x => x.task.GroupId!.Value)
                        .Select(g => new
                        {
                            GroupId = g.Key,
                            Count = g.Count()
                        })
                        .ToDictionaryAsync(x => x.GroupId, x => x.Count, stoppingToken);

                    var existingAnalytics = await context.GroupAnalytics
                        .Where(x => groups.Contains(x.GroupId) && x.Date == today)
                        .ToDictionaryAsync(x => x.GroupId, stoppingToken);

                    foreach (var groupId in groups)
                    {
                        var taskSnapshot = taskMetrics.GetValueOrDefault(groupId, new GroupTaskSnapshot(0, 0, 0));
                        var activeMembers = activeMembersByGroup.GetValueOrDefault(groupId, 0);
                        var messagesCount = messagesByGroup.GetValueOrDefault(groupId, 0);
                        var commentsCount = commentsByGroup.GetValueOrDefault(groupId, 0);

                        var completionRate = taskSnapshot.TotalTasks > 0
                            ? Math.Round((double)taskSnapshot.CompletedTasks / taskSnapshot.TotalTasks * 100, 2)
                            : 0;

                        if (existingAnalytics.TryGetValue(groupId, out var existing))
                        {
                            existing.TotalTasks = taskSnapshot.TotalTasks;
                            existing.CompletedTasks = taskSnapshot.CompletedTasks;
                            existing.OverdueTasks = taskSnapshot.OverdueTasks;
                            existing.ActiveMembers = activeMembers;
                            existing.MessagesCount = messagesCount;
                            existing.CommentsCount = commentsCount;
                            existing.CompletionRate = completionRate;
                            existing.UpdatedAt = DateTime.UtcNow;
                            continue;
                        }

                        context.GroupAnalytics.Add(new Models.Entities.GroupAnalytics
                        {
                            Id = Guid.NewGuid(),
                            GroupId = groupId,
                            Date = today,
                            TotalTasks = taskSnapshot.TotalTasks,
                            CompletedTasks = taskSnapshot.CompletedTasks,
                            OverdueTasks = taskSnapshot.OverdueTasks,
                            ActiveMembers = activeMembers,
                            MessagesCount = messagesCount,
                            CommentsCount = commentsCount,
                            CompletionRate = completionRate
                        });
                    }

                    await context.SaveChangesAsync(stoppingToken);

                    logger.LogInformation("Group analytics aggregation completed. Processed {Count} groups", groups.Count);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error during group analytics aggregation");
                }
            }

            logger.LogInformation("Group Analytics Job stopped");
        }
    }
}
