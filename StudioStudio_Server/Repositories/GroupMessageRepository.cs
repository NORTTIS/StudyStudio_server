using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    public class GroupMessageRepository : IGroupMessageRepository
    {
        private readonly StudioDbContext _context;
        private readonly ILogger<GroupMessageRepository> _logger;

        public GroupMessageRepository(StudioDbContext context, ILogger<GroupMessageRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<GroupMessage> AddAsync(GroupMessage message)
        {
            try
            {
                _logger.LogInformation("Adding GroupMessage: MessageId={MessageId}, GroupId={GroupId}, ParentMessageId={ParentId}", 
                    message.MessageId, message.GroupId, message.ParentMessageId);
                
                _context.GroupMessages.Add(message);
                var rowsAffected = await _context.SaveChangesAsync();
                
                _logger.LogInformation("GroupMessage saved: MessageId={MessageId}, RowsAffected={Rows}, ParentMessageId={ParentId}", 
                    message.MessageId, rowsAffected, message.ParentMessageId);
                
                return message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving GroupMessage: MessageId={MessageId}, ParentMessageId={ParentId}, Error={Error}", 
                    message.MessageId, message.ParentMessageId, ex.Message);
                throw;
            }
        }

        public async Task<GroupMessage?> GetByIdAsync(Guid messageId)
        {
            return await _context.GroupMessages
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.MessageId == messageId);
        }

        public async Task<GroupMessage?> GetByIdWithRepliesAsync(Guid messageId)
        {
            return await _context.GroupMessages
                .Include(m => m.User)
                .Include(m => m.Replies)
                    .ThenInclude(r => r.User)
                .Include(m => m.Replies)
                    .ThenInclude(r => r.Replies)
                        .ThenInclude(rr => rr.User)
                .FirstOrDefaultAsync(m => m.MessageId == messageId);
        }

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

            _logger.LogInformation("GetByGroupIdAsync: GroupId={GroupId}, Found {Count} parent messages", 
                groupId, messages.Count);
            
            foreach (var msg in messages)
            {
                _logger.LogInformation("Message {MessageId} has {ReplyCount} replies", 
                    msg.MessageId, msg.Replies?.Count ?? 0);
            }

            return messages;
        }

        public async Task<int> GetCountByGroupIdAsync(Guid groupId)
        {
            return await _context.GroupMessages
                .Where(m => m.GroupId == groupId && !m.IsDeleted && m.ParentMessageId == null)
                .CountAsync();
        }

        public async Task<int> GetReplyCountAsync(Guid messageId)
        {
            return await _context.GroupMessages
                .Where(m => m.ParentMessageId == messageId && !m.IsDeleted)
                .CountAsync();
        }

        public async Task SoftDeleteWithRepliesAsync(Guid messageId)
        {
            var message = await _context.GroupMessages
                .Include(m => m.Replies)
                    .ThenInclude(r => r.Replies)
                .FirstOrDefaultAsync(m => m.MessageId == messageId);

            if (message == null) return;

            message.IsDeleted = true;
            message.UpdatedAt = DateTime.UtcNow;

            SoftDeleteRepliesRecursive(message);

            await _context.SaveChangesAsync();
        }

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
