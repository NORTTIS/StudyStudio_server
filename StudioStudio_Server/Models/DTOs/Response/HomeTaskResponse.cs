namespace StudioStudio_Server.Models.DTOs.Response
{
    public class HomeTaskResponse
    {
        public List<TaskStatusDto> PersonalTaskStatuses { get; set; } = new();
        public List<AssignedGroupResponse> GroupTaskAssigned { get; set; } = new();
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
}
