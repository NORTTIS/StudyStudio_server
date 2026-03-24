namespace StudioStudio_Server.Models.Entities
{
    public class Studio
    {
        public Guid StudioId { get; set; }

        public string StudioName { get; set; } = null!;
        public string? Description { get; set; }

        public bool IsDeleted { get; set; } = false;

        public Guid OwnerId { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        // 🔹 ADDED: Studio personalization
        public string? AvatarUrl { get; set; }
        public string? ColorHex { get; set; }

        public ICollection<Group> Groups { get; set; } = new List<Group>();
        public ICollection<StudioParticipant> Participants { get; set; } = new List<StudioParticipant>();
    }
}