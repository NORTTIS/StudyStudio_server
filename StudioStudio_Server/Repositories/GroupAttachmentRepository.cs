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
    public class GroupAttachmentRepository(StudioDbContext context) : IGroupAttachmentRepository
    {
        private readonly StudioDbContext _context = context;

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

        public async Task<List<GroupAttachment>> GetByGroupIdPagedAsync(Guid groupId, int skip, int take)
        {
            return await _context.GroupAttachments
                .Where(a => a.GroupId == groupId && !a.IsDeleted)
                .OrderByDescending(a => a.UploadedAt)
                .Skip(skip)
                .Take(take)
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

        public async Task<long> GetTotalStorageUsedByGroupAsync(Guid groupId)
        {
            return await _context.GroupAttachments
                .Where(a => a.GroupId == groupId && !a.IsDeleted)
                .SumAsync(a => (long?)a.FileSize) ?? 0L;
        }

        public async Task HardDeleteAsync(Guid attachmentId)
        {
            await _context.GroupAttachments
                .Where(a => a.GroupAttachmentId == attachmentId)
                .ExecuteDeleteAsync();
        }

        public async Task HardDeleteManyAsync(List<Guid> attachmentIds)
        {
            if (attachmentIds.Count == 0) return;
            await _context.GroupAttachments
                .Where(a => attachmentIds.Contains(a.GroupAttachmentId))
                .ExecuteDeleteAsync();
        }

        public async Task<List<GroupAttachment>> GetStuckUploadsAsync(TimeSpan olderThan)
        {
            var cutoff = DateTime.UtcNow - olderThan;
            return await _context.GroupAttachments
                .Where(a => a.ProcessingStatus == DocumentStatus.Uploading && a.UploadedAt < cutoff)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
