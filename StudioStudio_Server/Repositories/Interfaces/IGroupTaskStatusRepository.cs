using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IGroupTaskStatusRepository
    {
        Task<List<GroupTaskStatus>> GetByGroupIdAsync(Guid groupId);
        Task AddAsync(GroupTaskStatus status);
        Task AddRangeAsync(List<GroupTaskStatus> statuses);
        Task<bool> ExistsAsync(Guid statusId);
        Task DeleteAsync(GroupTaskStatus status);
        Task UpdateAsync(GroupTaskStatus status);
        Task<GroupTaskStatus?> GetDetailAsync(Guid statusId);
        Task<List<GroupTaskStatus>> GetByIdsAndGroupIdAsync(List<Guid> statusIds, Guid groupId);
        Task SaveChangesAsync();
        Task<bool> NameExistsInGroupAsync(GroupTaskStatus taskStatus);
        Task<List<GroupTaskStatus>> GetByGroupIdWithTrackingAsync(Guid groupId);
        Task ReorderStatusAsync(Guid statusId, Guid? prevStatusId, Guid? nextStatusId, Guid groupId);
        Task<GroupTaskStatus?> FindNextAfterAsync(Guid groupId, long position);
        Task RebalanceColumnAsync(Guid groupId);
    }
}
