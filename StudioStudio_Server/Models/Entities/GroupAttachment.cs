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
    }
}