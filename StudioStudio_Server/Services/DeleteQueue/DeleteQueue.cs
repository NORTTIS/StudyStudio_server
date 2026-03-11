using System.Threading.Channels;

namespace StudioStudio_Server.Services.DeleteQueue
{
    /// <summary>
    /// Interface for managing vector deletion job queue
    /// Provides background processing for document deletion to avoid blocking HTTP requests
    /// </summary>
    public interface IDeleteQueue
    {
        /// <summary>
        /// Enqueue a new delete job
        /// </summary>
        ValueTask EnqueueAsync(DeleteJob job, CancellationToken cancellationToken = default);

        /// <summary>
        /// Dequeue next job for processing
        /// </summary>
        ValueTask<DeleteJob> DequeueAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get current job status
        /// </summary>
        DeleteJobStatusInfo? GetJobStatus(Guid attachmentId);

        /// <summary>
        /// Update job status
        /// </summary>
        void UpdateJobStatus(
            Guid attachmentId,
            DeleteJobStatus status,
            string? errorMessage = null,
            int deletedCount = 0,
            int failedCount = 0);

        /// <summary>
        /// Get queue depth
        /// </summary>
        int GetQueueDepth();
    }

    /// <summary>
    /// Channel-based implementation of delete queue
    /// High-performance, thread-safe queue using System.Threading.Channels
    /// 
    /// Processing Strategy:
    /// 1. Dequeue job from queue
    /// 2. Delete vectors with retry logic
    /// 3. Update status throughout processing
    /// 4. Handle partial failures gracefully
    /// </summary>
    public class DeleteQueue : IDeleteQueue, IDisposable
    {
        private readonly Channel<DeleteJob> _queue;
        private readonly Dictionary<Guid, DeleteJobStatusInfo> _jobStatuses = new();
        private readonly SemaphoreSlim _statusLock = new(1, 1);
        private readonly ILogger<DeleteQueue> _logger;
        private int _queueDepth = 0;
        private readonly Timer _cleanupTimer;
        private const int JOB_STATUS_RETENTION_HOURS = 24;

        public DeleteQueue(ILogger<DeleteQueue> logger)
        {
            _logger = logger;

            // Unbounded channel - queues all delete jobs
            var options = new UnboundedChannelOptions
            {
                SingleReader = true,  // Only one background service reading
                SingleWriter = false  // Multiple controllers can enqueue
            };

            _queue = Channel.CreateUnbounded<DeleteJob>(options);

            // Periodic cleanup of old job statuses (every 30 minutes)
            _cleanupTimer = new Timer(CleanupOldJobStatuses, null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));
        }

        private void CleanupOldJobStatuses(object? state)
        {
            _statusLock.Wait();
            try
            {
                var cutoff = DateTime.UtcNow.AddHours(-JOB_STATUS_RETENTION_HOURS);
                var keysToRemove = _jobStatuses
                    .Where(kvp => kvp.Value.CompletedAt.HasValue && kvp.Value.CompletedAt.Value < cutoff)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _jobStatuses.Remove(key);
                }

                if (keysToRemove.Count > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} old delete job statuses", keysToRemove.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old delete job statuses");
            }
            finally
            {
                _statusLock.Release();
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            _statusLock?.Dispose();
        }

        public async ValueTask EnqueueAsync(DeleteJob job, CancellationToken cancellationToken = default)
        {
            await _statusLock.WaitAsync(cancellationToken);
            try
            {
                // Initialize job status
                _jobStatuses[job.AttachmentId] = new DeleteJobStatusInfo
                {
                    AttachmentId = job.AttachmentId,
                    Status = DeleteJobStatus.Queued,
                    QueuedAt = job.QueuedAt,
                    TotalCount = job.ChunkCount,
                    RetryCount = job.RetryCount
                };

                await _queue.Writer.WriteAsync(job, cancellationToken);

                Interlocked.Increment(ref _queueDepth);

                _logger.LogInformation(
                    "?? Enqueued delete job: AttachmentId={AttachmentId}, File={FileName}, " +
                    "Chunks={ChunkCount}, Queue depth={Depth}",
                    job.AttachmentId, job.FileName, job.ChunkCount, _queueDepth);
            }
            finally
            {
                _statusLock.Release();
            }
        }

        public async ValueTask<DeleteJob> DequeueAsync(CancellationToken cancellationToken = default)
        {
            var job = await _queue.Reader.ReadAsync(cancellationToken);

            Interlocked.Decrement(ref _queueDepth);

            await _statusLock.WaitAsync(cancellationToken);
            try
            {
                if (_jobStatuses.TryGetValue(job.AttachmentId, out var status))
                {
                    status.Status = DeleteJobStatus.Processing;
                    status.StartedAt = DateTime.UtcNow;
                }
            }
            finally
            {
                _statusLock.Release();
            }

            _logger.LogInformation(
                "??? Dequeued delete job: AttachmentId={AttachmentId}, Remaining in queue={Depth}",
                job.AttachmentId, _queueDepth);

            return job;
        }

        public DeleteJobStatusInfo? GetJobStatus(Guid attachmentId)
        {
            _statusLock.Wait();
            try
            {
                return _jobStatuses.TryGetValue(attachmentId, out var status) ? status : null;
            }
            finally
            {
                _statusLock.Release();
            }
        }

        public void UpdateJobStatus(
            Guid attachmentId,
            DeleteJobStatus status,
            string? errorMessage = null,
            int deletedCount = 0,
            int failedCount = 0)
        {
            _statusLock.Wait();
            try
            {
                if (_jobStatuses.TryGetValue(attachmentId, out var jobStatus))
                {
                    jobStatus.Status = status;
                    jobStatus.ErrorMessage = errorMessage;
                    jobStatus.DeletedCount = deletedCount;
                    jobStatus.FailedCount = failedCount;

                    if (status == DeleteJobStatus.Completed ||
                        status == DeleteJobStatus.PartiallyCompleted ||
                        status == DeleteJobStatus.Failed)
                    {
                        jobStatus.CompletedAt = DateTime.UtcNow;
                    }

                    _logger.LogDebug(
                        "Delete job status updated: AttachmentId={AttachmentId}, Status={Status}, " +
                        "Progress={Deleted}/{Total}, Failed={Failed}",
                        attachmentId, status, deletedCount, jobStatus.TotalCount, failedCount);
                }
            }
            finally
            {
                _statusLock.Release();
            }
        }

        public int GetQueueDepth() => _queueDepth;
    }
}
