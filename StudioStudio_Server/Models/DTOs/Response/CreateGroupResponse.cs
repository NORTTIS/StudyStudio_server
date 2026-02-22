namespace StudioStudio_Server.Models.DTOs.Response
{
    public class CreateGroupResponse
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid? StudioId { get; set; }
        public string GroupType { get; set; } = string.Empty;
        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
