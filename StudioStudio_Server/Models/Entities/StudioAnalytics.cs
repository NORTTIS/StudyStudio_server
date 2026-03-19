namespace StudioStudio_Server.Models.Entities
{
    /// <summary>
    /// Daily studio analytics metrics
    /// </summary>
    public class StudioAnalytics
    {
        public Guid Id { get; set; }

        public Guid StudioId { get; set; }
        public DateOnly Date { get; set; }

        public int TotalGroups { get; set; }
        public int ActiveGroups { get; set; }
        public int TotalMembers { get; set; }
        public int ActiveMembers { get; set; }
        public int TasksCompleted { get; set; }
        public double OverallCompletionRate { get; set; }
        public double EngagementScore { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Studio? Studio { get; set; }
    }
}
