using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    /// <summary>
    /// Repository interface cho Report entity
    /// </summary>
    public interface IReportRepository
    {
        Task AddAsync(Report report);
    }
}
