using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface ITaskHistoryRepository
    {
        Task AddAsync(TaskHistory taskHistory);
    }
}
