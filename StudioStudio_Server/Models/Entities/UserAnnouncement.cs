namespace StudioStudio_Server.Models.Entities
{
    public class UserAnnouncement
    {
        public Guid UserAnnouncementId { get; set; }
        public Guid AnnouncementId { get; set; }
        public Guid MentionedId { get; set; }
        public Guid? CreatedBy { get; set; }
        public bool IsRead { get; set; } = false;
        public bool IsDelete { get; set; } = false;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Announcement? Announcement { get; set; } = null!;
    }
}