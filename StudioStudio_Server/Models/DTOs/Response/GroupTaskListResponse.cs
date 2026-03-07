using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Models.DTOs.Response
{
    /// <summary>
    /// Response DTO for group task list with pagination
    /// </summary>
    public class GroupTaskListResponse
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public List<GroupTaskItemResponse> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public List<TaskStatusInfoDto> GroupStatuses { get; set; } = new();
    }

    /// <summary>
    /// Individual task item in group task list
    /// </summary>
    public class GroupTaskItemResponse
    {
        public Guid TaskId { get; set; }
        public string TaskTitle { get; set; } = string.Empty;
        public string? TaskDescription { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public Guid StatusId { get; set; }
        public TaskPriority TaskPriority { get; set; }
        public TaskSeverity TaskSeverity { get; set; }
        public int Progress { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<UserDto> Assignees { get; set; } = new();
        public UserDto CreatedBy { get; set; } = new UserDto();
    }

    /// <summary>
    /// Task status information (ID and Name only)
    /// Used for filter dropdown in frontend
    /// </summary>
    public class TaskStatusInfoDto
    {
        public Guid StatusId { get; set; }
        public string StatusName { get; set; } = string.Empty;
    }
}
