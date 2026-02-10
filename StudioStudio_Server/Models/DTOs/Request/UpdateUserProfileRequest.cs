namespace StudioStudio_Server.Models.DTOs.Request
{
    public class UpdateUserProfileRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Bio { get; set; }
        public string? Language { get; set; }
        public bool? EmailNotificationEnabled { get; set; }
        public IFormFile? Avatar { get; set; }
    }
}
