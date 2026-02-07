namespace StudioStudio_Server.Models.Entities
{
    public class ActivityLog
    {
        public Guid LogId { get; set; }

        public Guid UserId { get; set; }
        public string ActionType { get; set; } = null!;
        public string TargetType { get; set; } = null!;

        public DateTime CreatedAt { get; set; }
    }
}
