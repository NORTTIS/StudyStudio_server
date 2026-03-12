namespace StudioStudio_Server.Models.DTOs.Response
{
    public class UserAnnouncementResponse
    {
        public Guid UserAnnouncementId { get; set; }
        public Guid AnnouncementId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? PublishedAt { get; set; }
        public Guid MentionedId { get; set; }   // Người được nhắc đến
        public Guid? CreatedBy { get; set; }   // Người tạo thông báo (có thể null)
    }
}
