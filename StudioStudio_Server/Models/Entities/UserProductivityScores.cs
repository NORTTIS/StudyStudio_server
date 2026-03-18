namespace StudioStudio_Server.Models.Entities
{
    /// <summary>
    /// Weekly productivity scores per user/group
    /// </summary>
    public class UserProductivityScores
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }
        public Guid? GroupId { get; set; }
        public DateOnly WeekStart { get; set; }

        public double ProductivityScore { get; set; }
        public int TasksCompleted { get; set; }
        public int TasksCreated { get; set; }
        public double OnTimeCompletionRate { get; set; }
        public double AverageTaskCompletionHours { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public User? User { get; set; }
        public Group? Group { get; set; }
    }
}
