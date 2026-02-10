namespace StudioStudio_Server.Models.DTOs.Request
{
    public class UserProfileUpdateRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Bio { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Language { get; set; }
        public bool? EmailNotificationEnabled { get; set; }
    }
}
