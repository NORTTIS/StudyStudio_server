using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    public class TaskAssignmentRepository : ITaskAssignmentRepository
    {
        private readonly StudioDbContext _db;
        public TaskAssignmentRepository(StudioDbContext db)
        {
            _db = db;
        }
        public async Task AddAsync(TaskAssignment assignment)
        {
            _db.TaskAssignments.Add(assignment);
            await _db.SaveChangesAsync();
        }
    }
}
