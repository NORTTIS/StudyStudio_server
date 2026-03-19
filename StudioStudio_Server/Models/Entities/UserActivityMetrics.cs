namespace StudioStudio_Server.Models.Entities
{
    /// <summary>
    /// Daily aggregated user activity metrics
    /// </summary>
    public class UserActivityMetrics
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }
        public DateOnly Date { get; set; }

        public int TasksCreated { get; set; }
        public int TasksCompleted { get; set; }
        public int CommentsPosted { get; set; }
        public int MessagesSent { get; set; }
        public int TotalActivityCount { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public User? User { get; set; }
    }
}
