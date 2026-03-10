using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Models.DTOs.Request
{
    public class GetReportsRequest
    {
        public string? SearchTerm { get; set; }
        public ReportType? Type { get; set; }
        public ReportStatus? Status { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
