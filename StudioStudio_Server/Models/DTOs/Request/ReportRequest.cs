namespace StudioStudio_Server.Models.DTOs.Request
{
    public class ReportRequest
    {
        public string Type { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}
