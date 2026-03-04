namespace StudioStudio_Server.Services.DeleteQueue
{
    /// <summary>
    /// Represents a vector deletion job to be processed in the background
    /// Used when deleting documents to avoid blocking HTTP requests
    /// </summary>
    public class DeleteJob
    {
        /// <summary>
        /// Document attachment ID
        /// </summary>
        public Guid AttachmentId { get; set; }

        /// <summary>
        /// Group ID (for logging and tracking)
        /// </summary>
        public Guid GroupId { get; set; }

        /// <summary>
        /// Number of chunks (vectors) to delete
        /// </summary>
        public int ChunkCount { get; set; }

        /// <summary>
        /// File name (for logging)
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// When the job was queued
        /// </summary>
        public DateTime QueuedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Number of retry attempts
        /// </summary>
        public int RetryCount { get; set; } = 0;

        /// <summary>
        /// Maximum retry attempts
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// User who initiated the delete
        /// </summary>
        public Guid UserId { get; set; }
    }
}
