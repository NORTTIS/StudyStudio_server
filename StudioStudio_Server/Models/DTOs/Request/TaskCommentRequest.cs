namespace StudioStudio_Server.Models.DTOs.Request
{
    public class SendTaskCommentRequest
    {
        public Guid TaskId { get; set; }
        public string Content { get; set; } = null!;
    }
}
