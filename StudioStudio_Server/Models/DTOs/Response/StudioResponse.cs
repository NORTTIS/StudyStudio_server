using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Models.DTOs.Response
{
    public class StudioResponse
    {
        public Guid StudioId { get; set; }
        public string StudioName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid OwnerId { get; set; }
        public StudioRole StudioRole { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int GroupCount { get; set; }
        public int MemberCount { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public string? AvatarUrl { get; set; }
        public string? ColorHex { get; set; }

        // 🔹 ADDED: Studio personalization
        public string? BannerUrl { get; set; }
        public string? Tagline { get; set; }
        public string? Alias { get; set; }

        // 🔹 ADDED: Open/Closed studio
        public bool IsOpen { get; set; } = true;
    }
}
