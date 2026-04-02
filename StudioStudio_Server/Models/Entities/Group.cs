namespace StudioStudio_Server.Models.Entities
{
    public class Group
    {
        public Guid GroupId { get; set; }

        public string GroupName { get; set; } = null!;
        public string? Description { get; set; }
        public Guid CreatedBy { get; set; }

        public Guid? StudioId { get; set; }
        public bool IsTemplate { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // 🔹 ADDED: Admin deactivate
        public bool IsActive { get; set; } = true;

        // 🔹 ADDED: Open/Closed group membership
        public bool IsOpen { get; set; } = true;

        // 🔹 ADDED: Group personalization
        public string? AvatarUrl { get; set; }
        public string? ColorHex { get; set; }
        public string? IconEmoji { get; set; }
        public string? BannerUrl { get; set; }
        public string? Tagline { get; set; }
        public string? Alias { get; set; }

        public ICollection<GroupParticipant> Participants { get; set; } = new List<GroupParticipant>();

        public ICollection<Favourite> Favourites { get; set; } = new List<Favourite>();


    }
}