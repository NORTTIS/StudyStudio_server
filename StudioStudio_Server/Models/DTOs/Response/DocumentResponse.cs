namespace StudioStudio_Server.Models.DTOs.Response
{
    /// <summary>
    /// Response cho document status
    /// </summary>
    public class DocumentStatusResponse
    {
        public Guid AttachmentId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int? ChunkCount { get; set; }
        public int? Progress { get; set; }
        public string? Message { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
    }

    /// <summary>
    /// Response danh sách documents trong group
    /// </summary>
    public class GroupDocumentsResponse
    {
        public List<DocumentItem> Documents { get; set; } = new();
        public int TotalCount { get; set; }
    }

    public class DocumentItem
    {
        public Guid AttachmentId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? ChunkCount { get; set; }
        public UserDto UploadedBy { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
