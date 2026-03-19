namespace StudioStudio_Server.Models.Entities
{
    /// <summary>
    /// Daily group analytics metrics
    /// </summary>
    public class GroupAnalytics
    {
        public Guid Id { get; set; }

        public Guid GroupId { get; set; }
        public DateOnly Date { get; set; }

        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int OverdueTasks { get; set; }
        public int ActiveMembers { get; set; }
        public int MessagesCount { get; set; }
        public int CommentsCount { get; set; }
        public double CompletionRate { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Group? Group { get; set; }
    }
}
