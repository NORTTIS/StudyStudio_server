namespace StudioStudio_Server.Services.CleanupQueue
{
    /// <summary>
    /// Represents a stuck upload cleanup job
    /// Created when a document upload was never completed (e.g. frontend crash, expired presigned URL)
    /// </summary>
    public class StuckUploadJob
    {
        /// <summary>
        /// Document attachment ID
        /// </summary>
        public Guid AttachmentId { get; set; }

        /// <summary>
        /// Group ID
        /// </summary>
        public Guid GroupId { get; set; }

        /// <summary>
        /// B2 file key (for deletion)
        /// </summary>
        public string FileKey { get; set; } = string.Empty;

        /// <summary>
        /// File name (for logging)
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// File size in bytes (for quota update)
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// When the upload was initiated
        /// </summary>
        public DateTime UploadedAt { get; set; }
    }
}
