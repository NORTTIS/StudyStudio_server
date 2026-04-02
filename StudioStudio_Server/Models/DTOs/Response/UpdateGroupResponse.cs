namespace StudioStudio_Server.Models.DTOs.Response
{
    public class UpdateGroupResponse
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid? StudioId { get; set; }
        public string GroupType { get; set; } = string.Empty;
        public bool IsTemplate { get; set; }
        public Guid? TemplateId { get; set; }
        public DateTime UpdatedAt { get; set; }

        // 🔹 ADDED: Group personalization
        public string? AvatarUrl { get; set; }
        public string? ColorHex { get; set; }
        public string? IconEmoji { get; set; }
        public string? BannerUrl { get; set; }
        public string? Tagline { get; set; }
        public string? Alias { get; set; }

        // 🔹 ADDED: Open/Closed group
        public bool IsOpen { get; set; }
    }
}
