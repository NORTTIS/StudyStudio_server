using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository x? l? c�c thao t�c CRUD v?i TaskComment entity
    /// </summary>
    public class TaskCommentRepository(StudioDbContext context) : ITaskCommentRepository
    {
        private readonly StudioDbContext _context = context;

        /// <summary>
        /// Th�m m?i m?t comment v�o database
        /// </summary>
        public async Task<TaskComment> AddAsync(TaskComment comment)
        {
            _context.TaskComments.Add(comment);
            await _context.SaveChangesAsync();
            return comment;
        }

        /// <summary>
        /// L?y comment theo ID (kh�ng load replies)
        /// �i?u ki?n: CommentId = {commentId}
        /// Include: User info
        /// </summary>
        public async Task<TaskComment?> GetByIdAsync(Guid commentId)
        {
            return await _context.TaskComments
                .Include(c => c.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CommentId == commentId);
        }

        /// <summary>
        /// L?y comment theo ID k�m t?t c? replies (nested up to 2 levels)
        /// �i?u ki?n: CommentId = {commentId}
        /// Include: User info, Replies ? User, Replies ? Replies ? User
        /// Use case: Load full comment thread khi delete (�? delete t?t c? replies)
        /// </summary>
        public async Task<TaskComment?> GetByIdWithRepliesAsync(Guid commentId)
        {
            return await _context.TaskComments
                .Include(c => c.User)
                .Include(c => c.Replies)
                    .ThenInclude(r => r.User)
                .Include(c => c.Replies)
                    .ThenInclude(r => r.Replies)
                        .ThenInclude(rr => rr.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CommentId == commentId);
        }

        /// <summary>
        /// L?y danh s�ch parent comments c?a task (pagination)
        /// �i?u ki?n: TaskId = {taskId} AND IsDeleted = false AND ParentCommentId = null
        /// Include: User info, Replies (1 level, ch? replies kh�ng b? x�a)
        /// S?p x?p: CreatedAt DESC (comment m?i nh?t tr�?c)
        /// Pagination: Skip({offset}).Take({limit})
        /// </summary>
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

        /// <summary>
        /// �?m t?ng s? parent comments c?a task
        /// �i?u ki?n: TaskId = {taskId} AND IsDeleted = false AND ParentCommentId = null
        /// </summary>
        public async Task<int> GetCountByTaskIdAsync(Guid taskId)
        {
            return await _context.TaskComments
                .Where(c => c.TaskId == taskId && !c.IsDeleted && c.ParentCommentId == null)
                .CountAsync();
        }

        /// <summary>
        /// �?m s? replies c?a m?t comment
        /// �i?u ki?n: ParentCommentId = {commentId} AND IsDeleted = false
        /// </summary>
        public async Task<int> GetReplyCountAsync(Guid commentId)
        {
            return await _context.TaskComments
                .Where(c => c.ParentCommentId == commentId && !c.IsDeleted)
                .CountAsync();
        }

        /// <summary>
        /// Soft delete comment v� t?t c? replies (recursive)
        /// Set IsDeleted = true cho comment v� t?t c? replies nested
        /// Update UpdatedAt = UtcNow
        /// </summary>
        public async Task SoftDeleteWithRepliesAsync(Guid commentId)
        {
            var comment = await _context.TaskComments
                .Include(c => c.Replies)
                    .ThenInclude(r => r.Replies)
                .FirstOrDefaultAsync(c => c.CommentId == commentId);

            if (comment == null)
            {
                return;
            }

            comment.IsDeleted = true;
            comment.UpdatedAt = DateTime.UtcNow;

            SoftDeleteRepliesRecursive(comment);

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Helper: Recursive soft delete t?t c? replies
        /// </summary>
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
