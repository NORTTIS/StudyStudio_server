using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Services.BackgroundServices
{
    /// <summary>
    /// Background job to track task-level performance metrics
    /// Runs daily at 01:00 to process task completion performance
    /// </summary>
    public class TaskPerformanceMetricsJob : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TaskPerformanceMetricsJob> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

        public TaskPerformanceMetricsJob(
            IServiceProvider serviceProvider,
            ILogger<TaskPerformanceMetricsJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Task Performance Metrics Job started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Calculate next run at 01:00
                    var now = DateTime.UtcNow;
                    var nextRun = now.Date.AddDays(1).AddHours(1);
                    var delay = nextRun - now;

                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, stoppingToken);
                    }

                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<StudioDbContext>();
                    var analyticsRepository = scope.ServiceProvider
                        .GetRequiredService<IAnalyticsRepository>();

                    _logger.LogInformation("Starting task performance metrics calculation...");

                    // Get all tasks that need performance metrics
                    // Tasks that have been completed or have due dates
                    var tasks = await context.Tasks
                        .Where(t => (t.CompletedAt.HasValue || t.DueDate.HasValue) &&
                                   t.Progress == 100)
                        .ToListAsync();

                    foreach (var task in tasks)
                    {
                        // Check if we already have metrics for this task
                        var existingMetrics = await analyticsRepository.GetTaskPerformanceAsync(task.TaskId);

                        if (existingMetrics != null)
                            continue; // Skip if already processed

                        var completedAt = task.CompletedAt;
                        var dueDate = task.DueDate;

                        // Calculate performance metrics
                        var completedOnTime = completedAt.HasValue && dueDate.HasValue && completedAt <= dueDate;

                        var daysEarlyOrLate = 0;
                        if (completedAt.HasValue && dueDate.HasValue)
                        {
                            daysEarlyOrLate = (completedAt.Value.Date - dueDate.Value.Date).Days;
                        }

                        // Calculate hour variance
                        var hourVariance = 0.0;
                        if (task.EstimatedHours.HasValue && task.ActualHours.HasValue)
                        {
                            hourVariance = (double)((task.ActualHours.Value - task.EstimatedHours.Value) / task.EstimatedHours.Value * 100);
                        }

                        var metrics = new Models.Entities.TaskPerformanceMetrics
                        {
                            Id = Guid.NewGuid(),
                            TaskId = task.TaskId,
                            UserId = task.OwnerId,
                            GroupId = task.GroupId,
                            EstimatedHours = task.EstimatedHours,
                            ActualHours = task.ActualHours,
                            HourVariance = Math.Round(hourVariance, 2),
                            CompletedOnTime = completedOnTime,
                            DaysEarlyOrLate = daysEarlyOrLate,
                            CompletedAt = completedAt,
                            DueDate = dueDate,
                            CreatedAt = task.CreatedAt,
                            UpdatedAt = DateTime.UtcNow
                        };

                        await analyticsRepository.UpsertTaskPerformanceAsync(metrics);
                    }

                    // Also update metrics for tasks that have changed
                    var tasksWithUpdates = await context.Tasks
                        .Where(t => t.CompletedAt.HasValue && t.Progress == 100)
                        .ToListAsync();

                    foreach (var task in tasksWithUpdates)
                    {
                        var existingMetrics = await analyticsRepository.GetTaskPerformanceAsync(task.TaskId);

                        if (existingMetrics == null)
                            continue;

                        var completedAt = task.CompletedAt;
                        var dueDate = task.DueDate;

                        var completedOnTime = completedAt.HasValue && dueDate.HasValue && completedAt <= dueDate;

                        var daysEarlyOrLate = 0;
                        if (completedAt.HasValue && dueDate.HasValue)
                        {
                            daysEarlyOrLate = (completedAt.Value.Date - dueDate.Value.Date).Days;
                        }

                        var hourVariance = 0.0;
                        if (task.EstimatedHours.HasValue && task.ActualHours.HasValue && task.EstimatedHours.Value > 0)
                        {
                            hourVariance = (double)((task.ActualHours.Value - task.EstimatedHours.Value) / task.EstimatedHours.Value * 100);
                        }

                        existingMetrics.EstimatedHours = task.EstimatedHours;
                        existingMetrics.ActualHours = task.ActualHours;
                        existingMetrics.HourVariance = Math.Round(hourVariance, 2);
                        existingMetrics.CompletedOnTime = completedOnTime;
                        existingMetrics.DaysEarlyOrLate = daysEarlyOrLate;
                        existingMetrics.CompletedAt = completedAt;
                        existingMetrics.DueDate = dueDate;
                        existingMetrics.UpdatedAt = DateTime.UtcNow;

                        await analyticsRepository.UpsertTaskPerformanceAsync(existingMetrics);
                    }

                    _logger.LogInformation("Task performance metrics calculation completed. Processed {Count} tasks", tasks.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during task performance metrics calculation");
                }
            }

            _logger.LogInformation("Task Performance Metrics Job stopped");
        }
    }
}
