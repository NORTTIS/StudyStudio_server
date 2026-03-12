using System;

namespace StudioStudio_Server.Models.Entities
{
    public class UserAnnouncement
    {
        public Guid UserAnnouncementId { get; set; }
        public Guid AnnouncementId { get; set; }
        public Guid MentionedId { get; set; }       // Người được nhắc đến
        public Guid? CreatedBy { get; set; }        // Người tạo thông báo (có thể null)
        public bool IsRead { get; set; } = false;
        public bool IsDelete { get; set; } = false;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}