namespace StudioStudio_Server.Models.DTOs.Response
{
    public class UserProfileResponse
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Bio { get; set; }
        public string? AvatarUrl { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        public string Language { get; set; } = string.Empty;
        public bool EmailNotificationEnabled { get; set; }
        public string? GoogleId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
