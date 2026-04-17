namespace StudioStudio_Server.Models.Entities
{
    public class StudioParticipant
    {
        public Guid ParticipantId { get; set; }

        public Guid StudioId { get; set; }
        public Guid UserId { get; set; }

        public StudioRole Role { get; set; }

        public DateTime CreatedAt { get; set; }

        // 🔹 ADDED: Approval status (true = auto-approved for open studios; must be approved for closed studios)
        public bool IsApproved { get; set; } = true;

        // Navigation properties
        public Studio Studio { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}
