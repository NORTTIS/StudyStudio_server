using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class TaskItemGroupRequest
    {
        public Guid GroupId { get; set; }
        public string TaskName { get; set; } = string.Empty;
        public string? TaskDescription { get; set; } = string.Empty;
        public TaskPriority TaskPriority { get; set; }
        public TaskSeverity TaskSeverity { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public Guid? GroupStatusId { get; set; }
        public Guid? PersonalStatusId { get; set; }
        public Guid? Assignees { get; set; }
        public decimal? EstimatedHours { get; set; }
        public decimal? ActualHours { get; set; }
    }

    public class TaskItemPersonalRequest
    {
        public string TaskName { get; set; } = string.Empty;
        public string? TaskDescription { get; set; } = string.Empty;
        public TaskPriority TaskPriority { get; set; }
        public TaskSeverity TaskSeverity { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public Guid? PersonalStatusId { get; set; }
        public decimal? EstimatedHours { get; set; }
        public decimal? ActualHours { get; set; }
    }

    public class ReorderTaskRequest
    {
        public Guid TaskId { get; set; }
        public Guid TargetStatusId { get; set; }
        public Guid? PrevTaskId { get; set; }
        public Guid? NextTaskId { get; set; }
    }
}
