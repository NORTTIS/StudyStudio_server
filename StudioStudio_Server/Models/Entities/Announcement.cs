using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Models.Entities
{
    public class Announcement
    {
        public Guid AnnouncementId { get; set; }
        public string Title { get; set; } = null!;
        public string Content { get; set; } = null!;
        public AnnouncementType Type { get; set; }
        public bool IsActive { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? PublishedAt { get; set; }

        // Context metadata for click-to-redirect
        public Guid? TaskId { get; set; }
        public Guid? GroupId { get; set; }
        public string? SourceType { get; set; } // e.g. "task", "discuss", "comment", "message"

        public List<UserAnnouncement> UserAnnouncements { get; set; } = new List<UserAnnouncement>();
    }
}
