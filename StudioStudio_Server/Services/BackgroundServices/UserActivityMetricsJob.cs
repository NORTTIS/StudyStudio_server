using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Services.BackgroundServices
{
    /// <summary>
    /// Background job to aggregate user daily activity metrics
    /// Runs every 10 minutes to process activity from the last interval
    /// </summary>
    public class UserActivityMetricsJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<UserActivityMetricsJob> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

        public UserActivityMetricsJob(
            IServiceProvider serviceProvider,
            ILogger<UserActivityMetricsJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("User Activity Metrics Job started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(Interval, stoppingToken);

                    using var scope = _serviceProvider.CreateScope();
                    var analyticsRepository = scope.ServiceProvider
                        .GetRequiredService<IAnalyticsRepository>();

                    _logger.LogInformation("Starting user activity metrics aggregation...");

                    var to = DateTime.UtcNow;
                    var from = to.AddMinutes(-10);

                    // Aggregate tasks created
                    var tasksCreated = await analyticsRepository.AggregateTasksCreatedByUserAsync(from, to);

                    // Aggregate tasks completed
                    var tasksCompleted = await analyticsRepository.AggregateTasksCompletedByUserAsync(from, to);

                    // Aggregate comments
                    var comments = await analyticsRepository.AggregateCommentsByUserAsync(from, to);

                    // Aggregate messages
                    var messages = await analyticsRepository.AggregateMessagesByUserAsync(from, to);

                    // Get all active users from the aggregations
                    var allUserIds = tasksCreated.Keys
                        .Union(tasksCompleted.Keys)
                        .Union(comments.Keys)
                        .Union(messages.Keys)
                        .Distinct();

                    var today = DateOnly.FromDateTime(DateTime.UtcNow);

                    foreach (var userId in allUserIds)
                    {
                        var metrics = new Models.Entities.UserActivityMetrics
                        {
                            Id = Guid.NewGuid(),
                            UserId = userId,
                            Date = today,
                            TasksCreated = tasksCreated.GetValueOrDefault(userId, 0),
                            TasksCompleted = tasksCompleted.GetValueOrDefault(userId, 0),
                            CommentsPosted = comments.GetValueOrDefault(userId, 0),
                            MessagesSent = messages.GetValueOrDefault(userId, 0),
                            TotalActivityCount = tasksCreated.GetValueOrDefault(userId, 0) +
                                               tasksCompleted.GetValueOrDefault(userId, 0) +
                                               comments.GetValueOrDefault(userId, 0) +
                                               messages.GetValueOrDefault(userId, 0)
                        };

                        await analyticsRepository.UpsertUserActivityAsync(metrics);
                    }

                    _logger.LogInformation("User activity metrics aggregation completed. Processed {Count} users", allUserIds.Count());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during user activity metrics aggregation");
                }
            }

            _logger.LogInformation("User Activity Metrics Job stopped");
        }
    }
}
