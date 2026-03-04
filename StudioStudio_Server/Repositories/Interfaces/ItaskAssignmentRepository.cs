using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface ITaskAssignmentRepository
    {
        Task AddRangeAsync(List<TaskAssignment> assignment);
        Task RemoveAsync(List<TaskAssignment> assignees);
        Task<List<TaskAssignment>> GetAssigneesByTaskId(Guid taskId);
        Task<Dictionary<Guid, List<TaskAssignment>>> GetListAssigneesByListTaskId(List<Guid> taskIds);
    }
}
