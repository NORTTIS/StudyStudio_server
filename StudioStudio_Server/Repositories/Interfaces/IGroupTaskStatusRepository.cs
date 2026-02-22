using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IGroupTaskStatusRepository
    {
        Task<List<GroupTaskStatus>> GetByGroupIdAsync(Guid groupId);
        Task AddAsync(GroupTaskStatus status);
        Task AddRangeAsync(List<GroupTaskStatus> statuses);
        Task<bool> ExistsAsync(Guid statusId);
    }
}
