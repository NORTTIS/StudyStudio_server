using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface ItaskAssignmentRepository
    {
        Task AddAsync(TaskAssignment assignment);
    }
}
