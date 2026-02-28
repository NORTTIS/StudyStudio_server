using StudioStudio_Server.Models.DTOs.Request;

namespace StudioStudio_Server.Services.Interfaces
{
    /// <summary>
    /// Service interface cho Reports (g?i báo cáo/feedback)
    /// </summary>
    public interface IReportService
    {
        Task SendReportAsync(Guid? userId, ReportRequest request);
    }
}
