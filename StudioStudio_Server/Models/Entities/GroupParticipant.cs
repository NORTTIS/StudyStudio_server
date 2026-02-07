namespace StudioStudio_Server.Models.Entities
{
    public class GroupParticipant
    {
        public Guid ParticipantId { get; set; }

        public Guid GroupId { get; set; }
        public Guid UserId { get; set; }

        public GroupRole Role { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}