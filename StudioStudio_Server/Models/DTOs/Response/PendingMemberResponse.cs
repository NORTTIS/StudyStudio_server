namespace StudioStudio_Server.Models.DTOs.Response
{
    public class PendingMemberListResponse
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public int TotalPending { get; set; }
        public List<PendingMemberDto> PendingMembers { get; set; } = new();
    }

    public class PendingMemberDto
    {
        public Guid UserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string Role { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
    }
}
