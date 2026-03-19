using System.Text.Json;

namespace StudioStudio_Server.Models.Entities
{
    public class ActivityLog
    {
        public Guid LogId { get; set; }

        public Guid UserId { get; set; }
        public string ActionType { get; set; } = null!;
        public string TargetType { get; set; } = null!;

        public Guid? TargetId { get; set; }
        public Guid? GroupId { get; set; }
        public Guid? StudioId { get; set; }

        public string? Metadata { get; set; }

        public DateTime CreatedAt { get; set; }

        // Navigation properties
        public virtual User? User { get; set; }
        public virtual Group? Group { get; set; }
        public virtual Studio? Studio { get; set; }
    }
}
