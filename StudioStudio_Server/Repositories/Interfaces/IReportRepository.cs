using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Repositories.Interfaces
{
    /// <summary>
    /// Repository interface for Report entity
    /// </summary>
    public interface IReportRepository
    {
        Task AddAsync(Report report);
        Task<List<Report>> GetReportsAsync(
            string? searchTerm,
            ReportType? type,
            ReportStatus? status,
            int pageNumber,
            int pageSize);
        Task<int> GetTotalReportsCountAsync(
            string? searchTerm,
            ReportType? type,
            ReportStatus? status);
        Task<int> GetReportsCountByStatusAsync(ReportStatus status);
        Task<Report?> GetReportByIdAsync(Guid reportId);
        Task UpdateAsync(Report report);
    }
}
