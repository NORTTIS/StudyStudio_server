namespace StudioStudio_Server.Services.EmbeddingQueue
{
    /// <summary>
    /// Job status for tracking embedding processing
    /// </summary>
    public enum EmbeddingJobStatus
    {
        /// <summary>
        /// Job is waiting in queue
        /// </summary>
        Queued,
        
        /// <summary>
        /// Job is currently being processed (extracting text, chunking, generating embeddings)
        /// Displayed to user as "Indexing"
        /// </summary>
        Processing,
        
        /// <summary>
        /// Job completed successfully
        /// </summary>
        Completed,
        
        /// <summary>
        /// Job failed with error
        /// </summary>
        Failed
    }

    /// <summary>
    /// Detailed status information for an embedding job
    /// </summary>
    public class EmbeddingJobStatusInfo
    {
        public Guid AttachmentId { get; set; }
        public EmbeddingJobStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime QueuedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        
        /// <summary>
        /// Number of chunks processed so far
        /// </summary>
        public int ProcessedChunks { get; set; }
        
        /// <summary>
        /// Total number of chunks to process
        /// </summary>
        public int TotalChunks { get; set; }
        
        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public int Progress => TotalChunks > 0 
            ? (int)((ProcessedChunks / (double)TotalChunks) * 100) 
            : 0;
        
        /// <summary>
        /// Number of retry attempts
        /// </summary>
        public int RetryCount { get; set; }
        
        /// <summary>
        /// Estimated tokens for this job
        /// </summary>
        public int EstimatedTokens { get; set; }
    }
}
