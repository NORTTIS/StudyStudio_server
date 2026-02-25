namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface ITaskRepository
    {
        Task<Dictionary<Guid, int>> GetTaskCountByGroupIdsAsync(List<Guid> groupIds);
        Task<int> GetTaskCountByGroupIdAsync(Guid groupId);
        Task<TaskItem?> GetByIdAsync(Guid taskId);
    }
}
