using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class TaskItemGroupRequest
    {
        public Guid GroupId { get; set; }
        public string TaskName { get; set; } = string.Empty;
        public string TaskDescription { get; set; } = string.Empty;
        public int TaskPriority { get; set; }
        public int TaskSeverity { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public Guid? GroupStatusId { get; set; }
        public Guid? PersonalStatusId { get; set; }
        public Guid? Assignees { get; set; }
    }

    public class ReorderTaskRequest
    {
        public Guid TaskId { get; set; }
        public Guid TargetStatusId { get; set; }
        public Guid? PrevTaskId { get; set; }
        public Guid? NextTaskId { get; set; }
    }
}
