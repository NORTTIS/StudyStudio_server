namespace StudioStudio_Server.Models.Entities
{
    public class TaskHistory
    {
        public Guid HistoryId { get; set; }

        public Guid TaskId { get; set; }
        public Guid StatusId { get; set; }

        public Guid ChangedBy { get; set; }
        public DateTime ChangedAt { get; set; }

        public string? ChangedContent { get; set; }
    }
}