using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Models.Entities
{
    public class GroupAttachment
    {
        public Guid GroupAttachmentId { get; set; }

        public Guid GroupId { get; set; }
        public Guid UploadedBy { get; set; }

        public string FileName { get; set; } = null!;
        public string FileType { get; set; } = null!;
        public long FileSize { get; set; }
        public string FileUrl { get; set; } = null!;

        public DateTime UploadedAt { get; set; }

        // Document processing fields
        public DocumentStatus? ProcessingStatus { get; set; }
        public int? ChunkCount { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsDeleted { get; set; } = false;

        // Navigation properties
        public virtual Group? Group { get; set; }
        public virtual User? Uploader { get; set; }
    }
}