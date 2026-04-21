using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Models.DTOs.Response
{
    public class PersonalTaskBoardResponse
    {
        public List<TaskStatusDto> PersonalTaskStatuses { get; set; } = new();
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
        public List<UserGroupDto> UserGroups { get; set; } = new();
    }

    public class HomeTaskListItemResponse
    {
        public Guid TaskId { get; set; }
        public string TaskTitle { get; set; } = string.Empty;
        public string SourceType { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;
        public Guid? GroupId { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public TaskSeverity TaskSeverity { get; set; }
        public TaskPriority TaskPriority { get; set; }
        public int Progress { get; set; }
        public DateTime? DueDate { get; set; }
        public string GroupName { get; set; } = string.Empty;
    }

    public class UserGroupDto
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
    }
}
