namespace StudioStudio_Server.Models.DTOs.Response
{
    public class AssignRoleResponse
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string OldRole { get; set; } = string.Empty;
        public string NewRole { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }
}
