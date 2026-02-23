namespace StudioStudio_Server.Models.DTOs.Response
{
    public class RemoveMemberResponse
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public Guid RemovedUserId { get; set; }
        public string RemovedUserName { get; set; } = string.Empty;
        public DateTime RemovedAt { get; set; }
    }
}
