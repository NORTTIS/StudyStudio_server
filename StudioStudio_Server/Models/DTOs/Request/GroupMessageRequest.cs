namespace StudioStudio_Server.Models.DTOs.Request
{
    public class SendGroupMessageRequest
    {
        public Guid GroupId { get; set; }
        public string Content { get; set; } = null!;
    }
}
