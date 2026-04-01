namespace StudioStudio_Server.Models.DTOs.Response
{
    public class LeaveGroupResponse
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public DateTime LeftAt { get; set; }
    }
}
