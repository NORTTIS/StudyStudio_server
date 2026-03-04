using Microsoft.EntityFrameworkCore;
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

        public async Task<List<TaskAssignment>> GetAssigneesByTaskId(Guid taskId)
        {
            return await _db.TaskAssignments
                .Where(t => t.TaskId == taskId)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task RemoveAsync(List<TaskAssignment> assignees)
        {
            foreach (var assignee in assignees)
            {
                _db.TaskAssignments.Remove(assignee);
            }
            await _db.SaveChangesAsync();
        }

        public async Task<Dictionary<Guid, List<TaskAssignment>>> GetListAssigneesByListTaskId(List<Guid> taskIds)
        {
            Dictionary<Guid, List<TaskAssignment>> result = new Dictionary<Guid, List<TaskAssignment>>();

            foreach (var taskId in taskIds)
            {
                var asssignee = await _db.TaskAssignments
                    .Where(a => a.TaskId == taskId)
                    .AsNoTracking()
                    .ToListAsync();

                result[taskId] = asssignee;
            }

            return result;
        }
    }
}
