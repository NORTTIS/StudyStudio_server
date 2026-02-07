namespace StudioStudio_Server.Models.Entities
{
    public class PersonalAttachment
    {
        public Guid AttachmentId { get; set; }

        public Guid UserId { get; set; }

        public string FileName { get; set; } = null!;
        public string FileType { get; set; } = null!;
        public long FileSize { get; set; }
        public string FileUrl { get; set; } = null!;

        public DateTime UploadedAt { get; set; }
    }
}