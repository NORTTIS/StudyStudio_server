using System.Text.Json;
using StackExchange.Redis;
using StudioStudio_Server.Models.BackgroundJobs;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services.TaskNotificationQueue
{
    public class TaskUpdateNotificationQueue : ITaskUpdateNotificationQueue
    {
        private const string QueueKey = "queue:task-update-notification";
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<TaskUpdateNotificationQueue> _logger;
        private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

        public TaskUpdateNotificationQueue(
            IConnectionMultiplexer redis,
            ILogger<TaskUpdateNotificationQueue> logger)
        {
            _redis = redis;
            _logger = logger;
        }

        public async ValueTask EnqueueAsync(TaskUpdateNotificationJob job, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var payload = JsonSerializer.Serialize(job, _serializerOptions);
            var db = _redis.GetDatabase();

            try
            {
                await db.ListRightPushAsync(QueueKey, payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue task update notification job: TaskId={TaskId}", job.TaskId);
                throw;
            }

            var depth = await db.ListLengthAsync(QueueKey);

            _logger.LogInformation(
                "Enqueued task update notification job: TaskId={TaskId}, GroupId={GroupId}, QueueDepth={QueueDepth}",
                job.TaskId, job.GroupId, depth);
        }

        public async ValueTask<TaskUpdateNotificationJob> DequeueAsync(CancellationToken cancellationToken = default)
        {
            var db = _redis.GetDatabase();

            while (!cancellationToken.IsCancellationRequested)
            {
                var item = await db.ListLeftPopAsync(QueueKey);
                if (item.HasValue)
                {
                    try
                    {
                        var job = JsonSerializer.Deserialize<TaskUpdateNotificationJob>(item!, _serializerOptions);
                        if (job == null)
                        {
                            _logger.LogWarning("Skipped empty task update notification payload from Redis queue");
                            continue;
                        }

                        return job;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to deserialize task update notification payload: {Payload}", item.ToString());
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }

            throw new OperationCanceledException(cancellationToken);
        }

        public int GetQueueDepth()
        {
            try
            {
                var db = _redis.GetDatabase();
                var depth = db.ListLength(QueueKey);
                return depth > int.MaxValue ? int.MaxValue : (int)depth;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get queue depth for {QueueKey}", QueueKey);
                return 0;
            }
        }
    }
}