namespace StudioStudio_Server.Models.DTOs.Response
{
    public class CreateGroupResponse
    {
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid? StudioId { get; set; }
        public string GroupType { get; set; } = string.Empty;
        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }

        // 🔹 FIX: Added missing personalization fields
        public string? AvatarUrl { get; set; }
        public string? ColorHex { get; set; }
        public string? IconEmoji { get; set; }
        public string? BannerUrl { get; set; }
        public string? Tagline { get; set; }
        public string? Alias { get; set; }

        // 🔹 ADDED: Open/Closed group
        public bool IsOpen { get; set; } = true;
    }
}
