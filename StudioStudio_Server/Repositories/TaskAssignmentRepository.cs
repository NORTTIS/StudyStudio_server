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
        public async Task AddRangeAsync(List<TaskAssignment> assignees)
        {
            foreach (var assignee in assignees)
            {
                _db.TaskAssignments.Add(assignee);
            }
            await _db.SaveChangesAsync();
        }
    }
}
