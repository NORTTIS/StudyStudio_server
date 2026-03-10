using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    /// <summary>
    /// Service interface cho Reports (g?i báo cáo/feedback)
    /// </summary>
    public interface IReportService
    {
        Task SendReportAsync(Guid? userId, ReportRequest request);
        Task<ReportListResponse> GetReportsAsync(GetReportsRequest request);
        Task<ReportItemResponse> UpdateReportAsync(Guid adminUserId, UpdateReportRequest request);
    }
}
