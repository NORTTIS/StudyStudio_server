namespace StudioStudio_Server.Services.EmbeddingQueue
{
    /// <summary>
    /// Represents a document embedding job to be processed in the background
    /// </summary>
    public class EmbeddingJob
    {
        public Guid AttachmentId { get; set; }
        public Guid UserId { get; set; }
        public Guid GroupId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
        public int RetryCount { get; set; } = 0;
        public int MaxRetries { get; set; } = 3;
        
        /// <summary>
        /// Estimated tokens for this document (calculated from file size)
        /// Used for token-based rate limiting
        /// </summary>
        public int EstimatedTokens { get; set; }
        
        /// <summary>
        /// Actual tokens used after processing
        /// Updated after document processing completes
        /// </summary>
        public int ActualTokens { get; set; }
        
        /// <summary>
        /// Priority of the job (lower number = higher priority)
        /// Can be used for prioritizing smaller documents
        /// </summary>
        public int Priority { get; set; } = 5;
    }
}
