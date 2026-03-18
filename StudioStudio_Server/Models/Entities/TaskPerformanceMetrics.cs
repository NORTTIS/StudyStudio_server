namespace StudioStudio_Server.Models.Entities
{
    /// <summary>
    /// Task-level performance metrics
    /// </summary>
    public class TaskPerformanceMetrics
    {
        public Guid Id { get; set; }

        public Guid TaskId { get; set; }
        public Guid UserId { get; set; }
        public Guid? GroupId { get; set; }

        public decimal? EstimatedHours { get; set; }
        public decimal? ActualHours { get; set; }
        public double HourVariance { get; set; }
        public bool CompletedOnTime { get; set; }
        public int DaysEarlyOrLate { get; set; }

        public DateTime? CompletedAt { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public TaskItem? Task { get; set; }
        public User? User { get; set; }
    }
}
