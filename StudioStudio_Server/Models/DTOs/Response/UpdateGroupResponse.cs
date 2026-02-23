namespace StudioStudio_Server.Models.DTOs.Response
{
    public class UpdateGroupResponse
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid? StudioId { get; set; }
        public string GroupType { get; set; } = string.Empty;
        public bool IsTemplate { get; set; }
        public Guid? TemplateId { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
