using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    public class TaskHistoryRepository : ITaskHistoryRepository
    {
        private readonly StudioDbContext _db;
        public TaskHistoryRepository(StudioDbContext db)
        {
            _db = db;
        }

        public async Task AddAsync(TaskHistory taskHistory)
        {
            _db.TaskHistories.Add(taskHistory);
            await _db.SaveChangesAsync();
        }

        public async Task<List<TaskHistory>> GetListTaskHistoryByTaskIdsAsync(List<Guid> taskIds)
        {
            List<TaskHistory> result = new List<TaskHistory>();
            foreach (var taskId in taskIds)
            {
                var taskHistories = await _db.TaskHistories.Where(h => h.TaskId == taskId).ToListAsync();
                result.AddRange(taskHistories);
            }

            return result;
        }
    }
}
