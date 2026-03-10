using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Models.DTOs.Response
{
    public class ReportListResponse
    {
        public ReportSummaryResponse Summary { get; set; } = null!;
        public List<ReportItemResponse> ReportList { get; set; } = new List<ReportItemResponse>();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
    }

    public class ReportSummaryResponse
    {
        public int TotalReport { get; set; }
        public int TotalOpen { get; set; }
        public int TotalInProgress { get; set; }
        public int TotalResolved { get; set; }
    }

    public class ReportItemResponse
    {
        public Guid ReportId { get; set; }
        public ReportType Type { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public ReportStatus Status { get; set; }
        public ReportPriority Priority { get; set; }
        public string? AdminNote { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid? UserId { get; set; }
    }
}
