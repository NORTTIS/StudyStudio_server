using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository x? l? c�c thao t�c CRUD v?i GroupMessage entity
    /// </summary>
    public class GroupMessageRepository(StudioDbContext context, ILogger<GroupMessageRepository> logger) : IGroupMessageRepository
    {
        private readonly StudioDbContext _context = context;
        private readonly ILogger<GroupMessageRepository> _logger = logger;

        /// <summary>
        /// Th�m m?i m?t message v�o database
        /// Include logging �? track message creation v� threading
        /// </summary>
        public async Task<GroupMessage> AddAsync(GroupMessage message)
        {
            try
            {
                _logger.LogInformation(
                    "Adding GroupMessage: MessageId={MessageId}, GroupId={GroupId}, ParentMessageId={ParentId}",
                    message.MessageId, message.GroupId, message.ParentMessageId);

                _context.GroupMessages.Add(message);
                var rowsAffected = await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "GroupMessage saved: MessageId={MessageId}, RowsAffected={Rows}, ParentMessageId={ParentId}",
                    message.MessageId, rowsAffected, message.ParentMessageId);

                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error saving GroupMessage: MessageId={MessageId}, ParentMessageId={ParentId}",
                    message.MessageId, message.ParentMessageId);
                throw;
            }
        }

        /// <summary>
        /// L?y message theo ID (kh�ng load replies)
        /// �i?u ki?n: MessageId = {messageId}
        /// Include: User info
        /// </summary>
        public async Task<GroupMessage?> GetByIdAsync(Guid messageId)
        {
            return await _context.GroupMessages
                .Include(m => m.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MessageId == messageId);
        }

        /// <summary>
        /// L?y message theo ID k�m t?t c? replies (nested up to 2 levels)
        /// �i?u ki?n: MessageId = {messageId}
        /// Include: User info, Replies ? User, Replies ? Replies ? User
        /// Use case: Load full conversation thread
        /// </summary>
        public async Task<GroupMessage?> GetByIdWithRepliesAsync(Guid messageId)
        {
            return await _context.GroupMessages
                .Include(m => m.User)
                .Include(m => m.Replies)
                    .ThenInclude(r => r.User)
                .Include(m => m.Replies)
                    .ThenInclude(r => r.Replies)
                        .ThenInclude(rr => rr.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MessageId == messageId);
        }

        /// <summary>
        /// L?y danh s�ch parent messages trong group (pagination)
        /// �i?u ki?n: GroupId = {groupId} AND IsDeleted = false AND ParentMessageId = null
        /// Include: User info, Replies (1 level, ch? replies kh�ng b? x�a)
        /// S?p x?p: CreatedAt DESC (tin nh?n m?i nh?t tr�?c)
        /// Pagination: Skip({offset}).Take({limit})
        /// </summary>
        public async Task<List<GroupMessage>> GetByGroupIdAsync(Guid groupId, int limit = 100, int offset = 0)
        {
            var messages = await _context.GroupMessages
                .Where(m => m.GroupId == groupId && !m.IsDeleted && m.ParentMessageId == null)
                .Include(m => m.User)
                .Include(m => m.Replies.Where(r => !r.IsDeleted))
                    .ThenInclude(r => r.User)
                .OrderByDescending(m => m.CreatedAt)
                .Skip(offset)
                .Take(limit)
                .AsNoTracking()
                .ToListAsync();

            _logger.LogInformation(
                "GetByGroupIdAsync: GroupId={GroupId}, Found {Count} parent messages",
                groupId, messages.Count);

            foreach (var msg in messages)
            {
                _logger.LogInformation(
                    "Message {MessageId} has {ReplyCount} replies",
                    msg.MessageId, msg.Replies?.Count ?? 0);
            }

            return messages;
        }

        /// <summary>
        /// �?m t?ng s? parent messages trong group
        /// �i?u ki?n: GroupId = {groupId} AND IsDeleted = false AND ParentMessageId = null
        /// </summary>
        public async Task<int> GetCountByGroupIdAsync(Guid groupId)
        {
            return await _context.GroupMessages
                .Where(m => m.GroupId == groupId && !m.IsDeleted && m.ParentMessageId == null)
                .CountAsync();
        }

        /// <summary>
        /// �?m s? replies c?a m?t message
        /// �i?u ki?n: ParentMessageId = {messageId} AND IsDeleted = false
        /// </summary>
        public async Task<int> GetReplyCountAsync(Guid messageId)
        {
            return await _context.GroupMessages
                .Where(m => m.ParentMessageId == messageId && !m.IsDeleted)
                .CountAsync();
        }

        /// <summary>
        /// Soft delete message v� t?t c? replies (recursive)
        /// Set IsDeleted = true cho message v� t?t c? replies nested
        /// Update UpdatedAt = UtcNow
        /// </summary>
        public async Task SoftDeleteWithRepliesAsync(Guid messageId)
        {
            var message = await _context.GroupMessages
                .Include(m => m.Replies)
                    .ThenInclude(r => r.Replies)
                .FirstOrDefaultAsync(m => m.MessageId == messageId);

            if (message == null)
            {
                return;
            }

            message.IsDeleted = true;
            message.UpdatedAt = DateTime.UtcNow;

            SoftDeleteRepliesRecursive(message);

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Helper: Recursive soft delete t?t c? replies
        /// </summary>
        private void SoftDeleteRepliesRecursive(GroupMessage message)
        {
            foreach (var reply in message.Replies)
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
