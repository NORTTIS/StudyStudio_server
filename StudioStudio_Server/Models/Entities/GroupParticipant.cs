namespace StudioStudio_Server.Models.Entities
{
    public class GroupParticipant
    {
        public Guid ParticipantId { get; set; }

        public Guid GroupId { get; set; }
        public Guid UserId { get; set; }

        public GroupRole Role { get; set; }

        public DateTime CreatedAt { get; set; }

        // 🔹 ADDED: Approval status (true = auto-approved for open groups; must be approved for closed groups)
        public bool IsApproved { get; set; } = true;

        public Group Group { get; set; } = null!;
    }
}