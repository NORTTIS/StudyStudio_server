using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface ITaskRepository
    {
        Task<Dictionary<Guid, int>> GetTaskCountByGroupIdsAsync(List<Guid> groupIds);
        Task<Dictionary<Guid, TaskSummaryResponse>> GetGroupTaskStatisticsBatchAsync(List<Guid> groupIds);
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
        Task<List<TaskItem>> GetPersonalTasksByOwnerAsync(Guid userId, int limit);
        Task<List<TaskItem>> GetPersonalTasksByOwnerWithDeadlineAsync(Guid userId, DateTime fromDate, DateTime toDate, int? limit = null);
        Task<List<TaskItem>> GetAssignedGroupTasksByUserAsync(Guid userId);
        Task<List<TaskItem>> GetAssignedGroupTasksByUserAsync(Guid userId, int limit);
        Task<(List<TaskItem> Tasks, int TotalCount)> GetAssignedGroupTasksWithPaginationAsync(
            Guid userId,
            int page,
            int pageSize,
            string? search = null,
            Guid? groupId = null,
            bool sortAscending = true);
        Task<(List<TaskItem> Tasks, int TotalCount)> GetGroupTasksWithFiltersAsync(
            Guid groupId,
            int page,
            int pageSize,
            string? search = null,
            Guid? assigneeId = null,
            Guid? statusId = null,
            TaskPriority? priority = null,
            TaskSeverity? severity = null,
            DateTime? startDateFrom = null,
            DateTime? startDateTo = null,
            DateTime? dueDateFrom = null,
            DateTime? dueDateTo = null,
            string? statusCategory = null,
            bool? hasNoAssignee = null,
            bool? hasNoDueDate = null,
            bool? overdue = null,
            string? sortBy = "createdAt",
            bool sortAscending = true,
            string? statusKeyword = null,
            TaskPriority? minPriority = null,
            TaskSeverity? minSeverity = null);
        Task PermanentDeleteAsync(Guid taskId);
        Task<Guid?> GetTaskGroupIdAsync(Guid taskId);
    }
}
