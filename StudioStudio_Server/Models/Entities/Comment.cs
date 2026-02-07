namespace StudioStudio_Server.Models.Entities
{
    public class Comment
    {
        public Guid CommentId { get; set; }

        public Guid TaskId { get; set; }
        public Guid UserId { get; set; }

        public string Content { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}