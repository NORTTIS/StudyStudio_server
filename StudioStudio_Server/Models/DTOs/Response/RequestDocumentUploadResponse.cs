namespace StudioStudio_Server.Models.DTOs.Response
{
    /// <summary>
    /// Response khi request document upload
    /// Ch?a presigned URL ð? frontend upload tr?c ti?p lên B2
    /// </summary>
    public class RequestDocumentUploadResponse
    {
        public Guid AttachmentId { get; set; }
        public string UploadUrl { get; set; } = string.Empty;
        public string FileKey { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }
}
