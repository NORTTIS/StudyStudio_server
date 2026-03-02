namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface ITaskRepository
    {
        Task<Dictionary<Guid, int>> GetTaskCountByGroupIdsAsync(List<Guid> groupIds);
        Task<int> GetTaskCountByGroupIdAsync(Guid groupId);
        Task<TaskItem?> GetByIdAsync(Guid taskId);
        Task AddAsync(TaskItem task);
        Task UpdateAsync(TaskItem task);
        Task SoftDeleteAsync(Guid taskId);
        Task RestoreAsync(Guid taskId);
        Task<List<TaskItem>> GetSoftDeleteTaskByGroup(Guid groupId);
    }
}
