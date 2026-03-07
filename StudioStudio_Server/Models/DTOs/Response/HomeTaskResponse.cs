namespace StudioStudio_Server.Models.DTOs.Response
{
    public class HomeTaskResponse
    {
        public List<TaskStatusDto> PersonalTaskStatuses { get; set; } = new();
        public List<AssignedGroupResponse> GroupTaskAssigned { get; set; } = new();
    }

    public class AssignedGroupResponse
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid? StudioId { get; set; }
        public string? StudioName { get; set; }
        public string GroupType { get; set; } = string.Empty;
        public bool IsTemplate { get; set; }
        public Guid? TemplateId { get; set; }
        public UserDto CreatedBy { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string UserRole { get; set; } = string.Empty;
        public List<TaskStatusDto> GroupTaskStatuses { get; set; } = new();
    }
}
