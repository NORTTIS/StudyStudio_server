namespace StudioStudio_Server.Models.Entities
{
    public class AIRequestLog
    {
        public Guid RequestId { get; set; }

        public Guid UserId { get; set; }
        public int TokenUsed { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}