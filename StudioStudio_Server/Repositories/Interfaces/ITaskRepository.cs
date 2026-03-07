using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface ITaskRepository
    {
        Task<Dictionary<Guid, int>> GetTaskCountByGroupIdsAsync(List<Guid> groupIds);
        Task<int> GetTaskCountByGroupIdAsync(Guid groupId);
        Task<TaskItem?> GetByIdAsync(Guid taskId);
        Task<TaskItem?> GetDeletedByIdAsync(Guid taskId);
        Task<TaskSummaryResponse> GetGroupTaskStatisticsAsync(Guid groupId);
        Task AddAsync(TaskItem task);
        Task UpdateAsync(TaskItem task);
        Task SoftDeleteAsync(Guid taskId);
        Task RestoreAsync(TaskItem task);
        Task<List<TaskItem>> GetSoftDeleteTaskByGroup(Guid groupId);
        Task<List<TaskItem>> GetAllTasksByStatusIdAsync(Guid statusId);
        Task<List<TaskItem>> GetAllPersonalTasksByStatusIdAsync(Guid statusId);
        Task<Dictionary<Guid, List<TaskItem>>> GetListTasksByListStatusId(List<Guid> listStatusIds);
        Task SaveChangesAsync();
        Task ReorderTaskAsync(Guid taskId, Guid targetStatusId, Guid? prevTaskId, Guid? nextTaskId);
        Task RebalanceTasksInStatusAsync(Guid statusId);
        Task<TaskItem?> FindNextAfterAsync(Guid statusId, long position);
        Task ReorderPersonalTaskAsync(Guid taskId, Guid targetStatusId, Guid? prevTaskId, Guid? nextTaskId);
        Task RebalancePersonalTasksInStatusAsync(Guid statusId);
        Task<Dictionary<Guid, List<TaskItem>>> GetPersonalListTasksByListStatusId(List<Guid> listStatusIds);
        Task<TaskItem?> PersonalFindNextAfterAsync(Guid statusId, long position);
        Task<List<TaskItem>> GetPersonalTasksByOwnerAsync(Guid userId);
        Task<List<TaskItem>> GetAssignedGroupTasksByUserAsync(Guid userId);
        Task PermanentDeleteAsync(Guid taskId);
    }
}
