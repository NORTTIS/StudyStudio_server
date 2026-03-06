using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class UpdateTaskRequest
    {
        public string? TaskName { get; set; }
        public string? TaskDescription { get; set; }
        public int? Progress { get; set; }
        public TaskPriority? TaskPriority { get; set; }
        public TaskSeverity? TaskSeverity { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public Guid? AssigneeId { get; set; }
        public Guid? GroupStatusId { get; set; }
    }
}
