namespace StudioStudio_Server.Models.DTOs.Response
{
    /// <summary>
    /// Response for document download URL request
    /// Contains presigned URL for direct download from B2
    /// </summary>
    public class DocumentDownloadUrlResponse
    {
        public Guid AttachmentId { get; set; }
        public string DownloadUrl { get; set; } = string.Empty;
        public int ExpiresIn { get; set; } // Expiration time in seconds
    }
}
