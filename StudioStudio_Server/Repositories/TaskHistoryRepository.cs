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
    }
}
