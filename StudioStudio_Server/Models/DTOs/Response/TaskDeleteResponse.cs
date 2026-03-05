namespace StudioStudio_Server.Models.DTOs.Response
{
    public class TaskDeleteResponse
    {
        public Guid DeleteTaskId { get; set; }
        public string TaskName { get; set; } = string.Empty;
        public DateTime DeletedOn { get; set; }
        public Guid DeletedBy { get; set; }
    }
}
