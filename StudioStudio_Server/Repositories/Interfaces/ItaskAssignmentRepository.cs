using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface ITaskAssignmentRepository
    {
        Task AddAsync(TaskAssignment assignment);
    }
}
