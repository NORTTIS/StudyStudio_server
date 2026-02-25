using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    public class TaskCommentRepository : ITaskCommentRepository
    {
        private readonly StudioDbContext _context;

        public TaskCommentRepository(StudioDbContext context)
        {
            _context = context;
        }

        public async Task<TaskComment> AddAsync(TaskComment comment)
        {
            _context.TaskComments.Add(comment);
            await _context.SaveChangesAsync();
            return comment;
        }

        public async Task<TaskComment?> GetByIdAsync(Guid commentId)
        {
            return await _context.TaskComments
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.CommentId == commentId);
        }

        public async Task<TaskComment?> GetByIdWithRepliesAsync(Guid commentId)
        {
            return await _context.TaskComments
                .Include(c => c.User)
                .Include(c => c.Replies)
                    .ThenInclude(r => r.User)
                .Include(c => c.Replies)
                    .ThenInclude(r => r.Replies)
                        .ThenInclude(rr => rr.User)
                .FirstOrDefaultAsync(c => c.CommentId == commentId);
        }

        public async Task<List<TaskComment>> GetByTaskIdAsync(Guid taskId, int limit = 100, int offset = 0)
        {
            return await _context.TaskComments
                .Where(c => c.TaskId == taskId && !c.IsDeleted && c.ParentCommentId == null)
                .Include(c => c.User)
                .Include(c => c.Replies.Where(r => !r.IsDeleted))
                    .ThenInclude(r => r.User)
                .OrderByDescending(c => c.CreatedAt)
                .Skip(offset)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<int> GetCountByTaskIdAsync(Guid taskId)
        {
            return await _context.TaskComments
                .Where(c => c.TaskId == taskId && !c.IsDeleted && c.ParentCommentId == null)
                .CountAsync();
        }

        public async Task<int> GetReplyCountAsync(Guid commentId)
        {
            return await _context.TaskComments
                .Where(c => c.ParentCommentId == commentId && !c.IsDeleted)
                .CountAsync();
        }

        public async Task SoftDeleteWithRepliesAsync(Guid commentId)
        {
            var comment = await _context.TaskComments
                .Include(c => c.Replies)
                    .ThenInclude(r => r.Replies)
                .FirstOrDefaultAsync(c => c.CommentId == commentId);

            if (comment == null) return;

            comment.IsDeleted = true;
            comment.UpdatedAt = DateTime.UtcNow;

            SoftDeleteRepliesRecursive(comment);

            await _context.SaveChangesAsync();
        }

        private void SoftDeleteRepliesRecursive(TaskComment comment)
        {
            foreach (var reply in comment.Replies)
            {
                reply.IsDeleted = true;
                reply.UpdatedAt = DateTime.UtcNow;

                if (reply.Replies.Any())
                {
                    SoftDeleteRepliesRecursive(reply);
                }
            }
        }
    }
}
