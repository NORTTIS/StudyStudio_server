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

        // NEW: Store task priority/severity at time of action for weighted contribution scoring
        public int? TaskPriority { get; set; }  // 0=Low, 1=Medium, 2=High
        public int? TaskSeverity { get; set; }  // 0=Minor, 1=Moderate, 2=Major, 3=Critical

        // Navigation properties
        public virtual User? User { get; set; }
        public virtual Group? Group { get; set; }
        public virtual Studio? Studio { get; set; }
    }
}
