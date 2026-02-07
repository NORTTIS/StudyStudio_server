namespace StudioStudio_Server.Models.Entities
{
    public class Studio
    {
        public Guid StudioId { get; set; }

        public string StudioName { get; set; } = null!;
        public string? Description { get; set; }

        public Guid OwnerId { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ICollection<Group> Groups { get; set; } = new List<Group>();
    }
}