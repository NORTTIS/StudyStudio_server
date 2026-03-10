using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Models.Entities
{
    public class Report
    {
        public Guid ReportId { get; set; }

        public Guid? UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = null!;

        public ReportType Type { get; set; }
        public ReportStatus Status { get; set; }
        public ReportPriority Priority { get; set; }

        public string? AdminNote { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}