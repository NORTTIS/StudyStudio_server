namespace StudioStudio_Server.Models.BackgroundJobs
{
    /// <summary>
    /// Represents a queued task update event used by the background notification worker.
    /// </summary>
    public class TaskUpdateNotificationJob
    {
        public Guid TaskId { get; set; }
        public Guid? GroupId { get; set; }
        public Guid ActorUserId { get; set; }
        public string TaskTitle { get; set; } = string.Empty;
        public DateTime? DueDate { get; set; }
        public Guid? OldAssigneeId { get; set; }
        public Guid? RequestedAssigneeId { get; set; }
        public bool HasAssigneeUpdate { get; set; }
        public bool ReachedCompletion { get; set; }
        public string? OldStatusName { get; set; }
        public string? NewStatusName { get; set; }
        public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
    }
}