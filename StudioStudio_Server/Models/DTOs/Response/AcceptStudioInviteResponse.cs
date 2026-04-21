namespace StudioStudio_Server.Models.DTOs.Response
{
    public class AcceptStudioInviteResponse
    {
        public Guid StudioId { get; set; }
        public string StudioName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsApproved { get; set; } = true;
        public DateTime JoinedAt { get; set; }
    }
}
