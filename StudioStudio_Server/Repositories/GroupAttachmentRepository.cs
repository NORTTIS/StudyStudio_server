using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository implementation cho Group Attachments
    /// </summary>
    public class GroupAttachmentRepository : IGroupAttachmentRepository
    {
        private readonly StudioDbContext _context;

        public GroupAttachmentRepository(StudioDbContext context)
        {
            _context = context;
        }

        public async Task<GroupAttachment?> GetByIdAsync(Guid attachmentId)
        {
            return await _context.GroupAttachments
                .FirstOrDefaultAsync(a => a.GroupAttachmentId == attachmentId && !a.IsDeleted);
        }

        public async Task<List<GroupAttachment>> GetByGroupIdAsync(Guid groupId)
        {
            return await _context.GroupAttachments
                .Where(a => a.GroupId == groupId && !a.IsDeleted)
                .OrderByDescending(a => a.UploadedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<GroupAttachment>> GetByGroupIdWithStatusAsync(Guid groupId, DocumentStatus status)
        {
            return await _context.GroupAttachments
                .Where(a => a.GroupId == groupId && 
                           a.ProcessingStatus == status && 
                           !a.IsDeleted)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<int> CountByGroupIdAsync(Guid groupId)
        {
            return await _context.GroupAttachments
                .CountAsync(a => a.GroupId == groupId && !a.IsDeleted);
        }

        public async Task CreateAsync(GroupAttachment attachment)
        {
            _context.GroupAttachments.Add(attachment);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(GroupAttachment attachment)
        {
            _context.GroupAttachments.Update(attachment);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> FileKeyExistsAsync(string fileKey)
        {
            return await _context.GroupAttachments
                .AnyAsync(a => a.FileUrl == fileKey && !a.IsDeleted);
        }
    }
}
