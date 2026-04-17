using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository handling operations with TaskAssignment entity
    /// Manages task-to-user assignments for group tasks
    /// Note: Personal tasks don't have assignments (owner = assignee by default)
    /// </summary>
    public class TaskAssignmentRepository(StudioDbContext db) : ITaskAssignmentRepository
    {
        private readonly StudioDbContext _db = db;

        /// <summary>
        /// Add new task assignment to database
        /// Creates assignment record linking task to assignee
        /// </summary>
        public async Task AddAsync(TaskAssignment assignee)
        {
            _db.TaskAssignments.Add(assignee);
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Get all assignees for a specific task
        /// Condition: TaskId = {taskId}
        /// Use case: Check who is assigned to task, update assignment
        /// </summary>
        public async Task<List<TaskAssignment>> GetAssigneesByTaskId(Guid taskId)
        {
            return await _db.TaskAssignments
                .Where(t => t.TaskId == taskId)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Remove task assignments (batch delete)
        /// Use case: Unassign user, change assignee, delete task
        /// </summary>
        public async Task RemoveAsync(List<TaskAssignment> assignees)
        {
            foreach (var assignee in assignees)
            {
                _db.TaskAssignments.Remove(assignee);
            }
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Get assignees for multiple tasks (batch query)
        /// Condition: TaskId IN {taskIds}
        /// Returns: Empty list if taskIds is null or empty
        /// Use case: Load assignee info for list of tasks in group detail
        /// </summary>
        public async Task<List<TaskAssignment>> GetListAssigneesByListTaskId(List<Guid> taskIds)
        {
            if (taskIds == null || taskIds.Count == 0)
                return new List<TaskAssignment>();

            return await _db.TaskAssignments
                .Where(a => taskIds.Contains(a.TaskId))
                .AsNoTracking()
                .ToListAsync();
        }
        
        /// <summary>
        /// Get all task assignments for a user
        /// Condition: AssignedTo = {userId}
        /// Use case: Get list of tasks assigned to user
        /// </summary>
        public async Task<List<TaskAssignment>> GetListTaskIdByUserIdAsync(Guid userId)
        {
            return await _db.TaskAssignments
                .Where(a => a.AssignedTo == userId)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
