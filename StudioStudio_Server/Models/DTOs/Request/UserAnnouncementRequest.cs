namespace StudioStudio_Server.Models.DTOs.Request
{
    public class UserAnnouncementRequest
    {
        public Guid AnnouncementId { get; set; }
        public Guid MentionedId { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
