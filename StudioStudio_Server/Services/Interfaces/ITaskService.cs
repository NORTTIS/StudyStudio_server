using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface ITaskService
    {
        Task<TaskItemResponse> AddGroupTaskAsync(Guid userId, TaskItemGroupRequest request);
        Task SoftDeleteTaskAsync(Guid userId, Guid groupId, Guid taskId);
        Task RestoreGroupTaskAsync(Guid userId, Guid groupId, Guid taskId);
        Task<List<TaskDeleteResponse>> GetDeleteTaskListAsync(Guid userId, Guid groupId);
        Task ReorderTaskAsync(Guid userId, Guid groupId, ReorderTaskRequest request);

    }
}
