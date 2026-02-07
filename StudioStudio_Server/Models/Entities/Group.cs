namespace StudioStudio_Server.Models.Entities
{
    public class Group
    {
        public Guid GroupId { get; set; }

        public string GroupName { get; set; } = null!;
        public Guid CreatedBy { get; set; }

        public Guid? StudioId { get; set; }
        public bool IsTemplate { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // 🔹 ADDED: Admin deactivate
        public bool IsActive { get; set; } = true;

        public ICollection<GroupParticipant> Participants { get; set; } = new List<GroupParticipant>();
    }
}