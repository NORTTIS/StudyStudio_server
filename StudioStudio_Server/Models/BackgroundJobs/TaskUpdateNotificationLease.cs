namespace StudioStudio_Server.Models.BackgroundJobs
{
    /// <summary>
    /// Represents a leased task update notification payload read from Redis but not yet acknowledged.
    /// </summary>
    public sealed class TaskUpdateNotificationLease
    {
        public required string Payload { get; init; }

        public required TaskUpdateNotificationJob Job { get; init; }
    }
}