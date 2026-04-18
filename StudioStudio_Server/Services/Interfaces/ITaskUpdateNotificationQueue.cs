using StudioStudio_Server.Models.BackgroundJobs;
using StudioStudio_Server.Services.TaskNotificationQueue;

namespace StudioStudio_Server.Services.Interfaces
{
    /// <summary>
    /// Contract for durable task update notification queue operations.
    /// Validate: <paramref name="job"/> must contain required identifiers before enqueue.
    /// Returns: Queue operation completion and current dequeue payload when available.
    /// </summary>
    public interface ITaskUpdateNotificationQueue
    {
        /// <summary>
        /// Enqueue a task update notification job.
        /// Validate: job payload is not null and contains TaskId/ActorUserId.
        /// Returns: Completed value task when the message is persisted.
        /// </summary>
        ValueTask EnqueueAsync(TaskUpdateNotificationJob job, CancellationToken cancellationToken = default);

        /// <summary>
        /// Dequeue the next task update notification job.
        /// Validate: caller should pass cancellation token to stop worker gracefully.
        /// Returns: The next queued notification job.
        /// </summary>
        /// <summary>
        /// Dequeue the next task update notification job as a lease.
        /// Validate: caller must acknowledge or abandon the lease after processing.
        /// Returns: The next leased notification item, or null when the queue is empty.
        /// </summary>
        ValueTask<TaskUpdateNotificationLease?> DequeueAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Acknowledge a leased notification payload after successful processing.
        /// Validate: payload must match a leased item from the inflight list.
        /// Returns: Completed when the payload is removed from inflight storage.
        /// </summary>
        ValueTask AcknowledgeAsync(TaskUpdateNotificationLease lease, CancellationToken cancellationToken = default);

        /// <summary>
        /// Requeue a leased notification payload after processing failure.
        /// Validate: payload must match a leased item from the inflight list.
        /// Returns: Completed when the payload is moved back to the pending queue.
        /// </summary>
        ValueTask AbandonAsync(TaskUpdateNotificationLease lease, CancellationToken cancellationToken = default);
    }
}