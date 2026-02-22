namespace StudioStudio_Server.Models.Entities
{
    public class Template
    {
        public Guid TemplateId { get; set; }
        public Guid UserId { get; set; }
        public Guid GroupId { get; set; }
        public bool IsSystemTemplate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        public User User { get; set; } = null!;
        public Group Group { get; set; } = null!;
    }
}
