using System.Text.Json;
using StackExchange.Redis;
using StudioStudio_Server.Models.BackgroundJobs;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services.TaskNotificationQueue
{
    public class TaskUpdateNotificationQueue(
        IConnectionMultiplexer redis,
        ILogger<TaskUpdateNotificationQueue> logger) : ITaskUpdateNotificationQueue
    {
        private const string PendingQueueKey = "queue:task-update-notification:pending";
        private const string InflightQueueKey = "queue:task-update-notification:inflight";
        private readonly IConnectionMultiplexer _redis = redis;
        private readonly ILogger<TaskUpdateNotificationQueue> _logger = logger;
        private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

        public async ValueTask EnqueueAsync(TaskUpdateNotificationJob job, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var payload = JsonSerializer.Serialize(job, _serializerOptions);
            var db = _redis.GetDatabase();

            try
            {
                await db.ListRightPushAsync(PendingQueueKey, payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue task update notification job: TaskId={TaskId}", job.TaskId);
                throw;
            }

            long depth;
            try
            {
                depth = await db.ListLengthAsync(PendingQueueKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read queue depth after enqueue for {QueueKey}", PendingQueueKey);
                depth = -1;
            }

            if (depth >= 0)
            {
                _logger.LogInformation(
                    "Enqueued task update notification job: TaskId={TaskId}, GroupId={GroupId}, QueueDepth={QueueDepth}",
                    job.TaskId, job.GroupId, depth);
            }
            else
            {
                _logger.LogInformation(
                    "Enqueued task update notification job: TaskId={TaskId}, GroupId={GroupId}",
                    job.TaskId, job.GroupId);
            }
        }

        public async ValueTask<TaskUpdateNotificationLease?> DequeueAsync(CancellationToken cancellationToken = default)
        {
            var db = _redis.GetDatabase();

            while (!cancellationToken.IsCancellationRequested)
            {
                var item = await db.ListRightPopLeftPushAsync(PendingQueueKey, InflightQueueKey);
                if (item.HasValue)
                {
                    try
                    {
                        var job = JsonSerializer.Deserialize<TaskUpdateNotificationJob>(item!, _serializerOptions);
                        if (job == null)
                        {
                            await db.ListRemoveAsync(InflightQueueKey, item!);
                            _logger.LogWarning("Skipped empty task update notification payload from Redis queue");
                            continue;
                        }

                        return new TaskUpdateNotificationLease
                        {
                            Payload = item!,
                            Job = job
                        };
                    }
                    catch (Exception ex)
                    {
                        await db.ListRemoveAsync(InflightQueueKey, item!);
                        _logger.LogError(ex, "Failed to deserialize task update notification payload from Redis queue");
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }

            throw new OperationCanceledException(cancellationToken);
        }

        public async ValueTask AcknowledgeAsync(TaskUpdateNotificationLease lease, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var db = _redis.GetDatabase();
            await db.ListRemoveAsync(InflightQueueKey, lease.Payload);
        }

        public async ValueTask AbandonAsync(TaskUpdateNotificationLease lease, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var db = _redis.GetDatabase();

            var removed = await db.ListRemoveAsync(InflightQueueKey, lease.Payload);
            if (removed > 0)
            {
                await db.ListLeftPushAsync(PendingQueueKey, lease.Payload);
                return;
            }

            _logger.LogWarning("Unable to requeue task update notification lease because inflight payload was not found");
        }

        public int GetQueueDepth()
        {
            try
            {
                var db = _redis.GetDatabase();
                var depth = db.ListLength(PendingQueueKey);
                return depth > int.MaxValue ? int.MaxValue : (int)depth;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get queue depth for {QueueKey}", PendingQueueKey);
                return 0;
            }
        }
    }
}