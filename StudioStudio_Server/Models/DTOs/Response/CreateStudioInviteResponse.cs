namespace StudioStudio_Server.Models.DTOs.Response
{
    public class CreateStudioInviteResponse
    {
        public string InviteUrl { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
