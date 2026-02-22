namespace StudioStudio_Server.Models.DTOs.Response
{
    public class TemplateResponse
    {
        public Guid TemplateId { get; set; }
        public Guid UserId { get; set; }
        public Guid GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string? GroupDescription { get; set; }
        public bool IsSystemTemplate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
