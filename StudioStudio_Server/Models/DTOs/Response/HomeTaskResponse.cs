namespace StudioStudio_Server.Models.DTOs.Response
{
    public class PersonalTaskBoardResponse
    {
        public List<TaskStatusDto> PersonalTaskStatuses { get; set; } = new();
    }

    public class AssignedGroupResponse
    {
        public Guid TaskId { get; set; }
        public string TaskName { get; set; } = string.Empty;
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
        public int Priority { get; set; }
        public int Severity { get; set; }
        public DateTime? DueDate { get; set; }
    }

    public class HomeSummaryResponse
    {
        public int RemainingTaskCount { get; set; }
        public int OverdueTaskCount { get; set; }
        public int CompletedTaskCount { get; set; }
        public int TotalJoinedGroupCount { get; set; }
    }

    public class HomeTaskListResponse
    {
        public List<HomeTaskListItemResponse> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class HomeTaskListItemResponse
    {
        public Guid TaskId { get; set; }
        public string TaskTitle { get; set; } = string.Empty;
        public string SourceType { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;
        public Guid? GroupId { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public Models.Enums.TaskSeverity TaskSeverity { get; set; }
        public Models.Enums.TaskPriority TaskPriority { get; set; }
        public int Progress { get; set; }
        public DateTime? DueDate { get; set; }
    }
}
