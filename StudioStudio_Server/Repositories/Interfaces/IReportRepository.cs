using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    /// <summary>
    /// Repository interface for Report entity
    /// </summary>
    public interface IReportRepository
    {
        Task AddAsync(Report report);
    }
}
