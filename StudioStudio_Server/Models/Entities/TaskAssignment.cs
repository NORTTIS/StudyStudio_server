namespace StudioStudio_Server.Models.Entities
{
    public class TaskAssignment
    {
        public Guid AssignmentId { get; set; }

        public Guid TaskId { get; set; }
        public Guid AssignedTo { get; set; }
        public Guid AssignedBy { get; set; }

        public DateTime AssignedAt { get; set; }
    }
}