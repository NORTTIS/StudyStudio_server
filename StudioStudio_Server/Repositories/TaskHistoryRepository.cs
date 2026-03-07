using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository handling operations with TaskHistory entity
    /// Tracks changes made to tasks (create, update, delete, status changes)
    /// Used for audit trail and task recovery
    /// </summary>
    public class TaskHistoryRepository : ITaskHistoryRepository
    {
        private readonly StudioDbContext _db;
        
        public TaskHistoryRepository(StudioDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Add new task history record to database
        /// Records who made changes and when
        /// </summary>
        public async Task AddAsync(TaskHistory taskHistory)
        {
            _db.TaskHistories.Add(taskHistory);
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Get task history records for multiple tasks (batch query)
        /// Condition: TaskId IN {taskIds}
        /// Use case: Display deletion history for soft-deleted tasks
        /// Returns: All history records for the given tasks
        /// </summary>
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
