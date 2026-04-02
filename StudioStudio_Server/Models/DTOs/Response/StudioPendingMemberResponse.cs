namespace StudioStudio_Server.Models.DTOs.Response
{
    public class StudioPendingMemberListResponse
    {
        public Guid StudioId { get; set; }
        public string StudioName { get; set; } = string.Empty;
        public int TotalPending { get; set; }
        public List<StudioPendingMemberDto> PendingMembers { get; set; } = new();
    }

    public class StudioPendingMemberDto
    {
        public Guid UserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public DateTime RequestedAt { get; set; }
    }
}
