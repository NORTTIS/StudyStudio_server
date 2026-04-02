namespace StudioStudio_Server.Models.DTOs.Request
{
    public class UpdateStudioRequest
    {
        public Guid Id { get; set; }
        public string StudioName { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        // 🔹 ADDED: Studio personalization
        public string? AvatarUrl { get; set; }
        public string? ColorHex { get; set; }
        public string? BannerUrl { get; set; }
        public string? Tagline { get; set; }
        public string? Alias { get; set; }

        // 🔹 ADDED: Open/Closed studio
        public bool? IsOpen { get; set; }
    }
}
