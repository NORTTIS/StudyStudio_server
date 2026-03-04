using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface ITaskRepository
    {
        Task<Dictionary<Guid, int>> GetTaskCountByGroupIdsAsync(List<Guid> groupIds);
        Task<int> GetTaskCountByGroupIdAsync(Guid groupId);
        Task<TaskItem?> GetByIdAsync(Guid taskId);
        Task<TaskSummaryResponse> GetGroupTaskStatisticsAsync(Guid groupId);
        Task AddAsync(TaskItem task);
        Task UpdateAsync(TaskItem task);
        Task SoftDeleteAsync(Guid taskId);
        Task RestoreAsync(TaskItem task);
        Task<List<TaskItem>> GetSoftDeleteTaskByGroup(Guid groupId);
        Task<List<TaskItem>> GetAllTasksByStatusIdAsync(Guid statusId);
        Task SaveChangesAsync();
    }
}
