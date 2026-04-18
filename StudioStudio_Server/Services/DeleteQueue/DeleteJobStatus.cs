namespace StudioStudio_Server.Services.DeleteQueue
{
    /// <summary>
    /// Job status for tracking vector deletion
    /// </summary>
    public enum DeleteJobStatus
    {
        /// <summary>
        /// Job is waiting in queue
        /// </summary>
        Queued,

        /// <summary>
        /// Job is currently being processed (deleting vectors)
        /// </summary>
        Processing,

        /// <summary>
        /// Job completed successfully (all vectors deleted)
        /// </summary>
        Completed,

        /// <summary>
        /// Job partially completed (some vectors failed to delete)
        /// </summary>
        PartiallyCompleted,

        /// <summary>
        /// Job failed completely
        /// </summary>
        Failed
    }

    /// <summary>
    /// Detailed status information for a delete job
    /// </summary>
    public class DeleteJobStatusInfo
    {
        public Guid AttachmentId { get; set; }
        public DeleteJobStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime QueuedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Number of vectors deleted successfully
        /// </summary>
        public int DeletedCount { get; set; }

        /// <summary>
        /// Total number of vectors to delete
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Number of vectors that failed to delete
        /// </summary>
        public int FailedCount { get; set; }

        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public int Progress => TotalCount > 0
            ? (int)((DeletedCount / (double)TotalCount) * 100)
            : 0;

        /// <summary>
        /// Number of retry attempts
        /// </summary>
        public int RetryCount { get; set; }
    }
}
