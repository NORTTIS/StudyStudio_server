namespace StudioStudio_Server.Models.DTOs.Response
{
    public class AcceptInviteLinkResponse
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        // 🔹 ADDED: True = joined directly; False = pending approval (closed group)
        public bool IsApproved { get; set; } = true;
        public DateTime JoinedAt { get; set; }
    }
}
