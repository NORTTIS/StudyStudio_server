using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface ITaskAssignmentRepository
    {
        Task AddAsync(TaskAssignment assignment);
        Task RemoveAsync(List<TaskAssignment> assignees);
        Task<List<TaskAssignment>> GetAssigneesByTaskId(Guid taskId);
        Task<List<TaskAssignment>> GetListAssigneesByListTaskId(List<Guid> taskIds);
        Task<List<TaskAssignment>> GetListTaskIdByUserIdAsync(Guid userId);
    }
}
