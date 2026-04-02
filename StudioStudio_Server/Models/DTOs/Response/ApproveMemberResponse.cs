namespace StudioStudio_Server.Models.DTOs.Response
{
    public class ApproveMemberResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public bool IsApproved { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
