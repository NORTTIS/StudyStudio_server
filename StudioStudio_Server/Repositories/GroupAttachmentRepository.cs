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
        public async Task<GroupAttachment?> GetByIdAsync(Guid attachmentId)
        {
            return await context.GroupAttachments
                .FirstOrDefaultAsync(a => a.GroupAttachmentId == attachmentId && !a.IsDeleted);
        }

        public async Task<List<GroupAttachment>> GetByGroupIdAsync(Guid groupId)
        {
            return await context.GroupAttachments
                .Where(a => a.GroupId == groupId && !a.IsDeleted)
                .OrderByDescending(a => a.UploadedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<GroupAttachment>> GetByGroupIdPagedAsync(Guid groupId, int skip, int take)
        {
            return await context.GroupAttachments
                .Where(a => a.GroupId == groupId && !a.IsDeleted)
                .OrderByDescending(a => a.UploadedAt)
                .Skip(skip)
                .Take(take)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<int> CountByGroupIdAsync(Guid groupId)
        {
            return await context.GroupAttachments
                .CountAsync(a => a.GroupId == groupId && !a.IsDeleted);
        }

        public async Task CreateAsync(GroupAttachment attachment)
        {
            context.GroupAttachments.Add(attachment);
            await context.SaveChangesAsync();
        }

        public async Task UpdateAsync(GroupAttachment attachment)
        {
            context.GroupAttachments.Update(attachment);
            await context.SaveChangesAsync();
        }


        public async Task<long> GetTotalStorageUsedByGroupAsync(Guid groupId)
        {
            return await context.GroupAttachments
                .Where(a => a.GroupId == groupId && !a.IsDeleted)
                .SumAsync(a => (long?)a.FileSize) ?? 0L;
        }

        public async Task HardDeleteAsync(Guid attachmentId)
        {
            await context.GroupAttachments
                .Where(a => a.GroupAttachmentId == attachmentId)
                .ExecuteDeleteAsync();
        }


        public async Task<List<GroupAttachment>> GetStuckUploadsAsync(TimeSpan olderThan)
        {
            var cutoff = DateTime.UtcNow - olderThan;
            return await context.GroupAttachments
                .Where(a => a.ProcessingStatus == DocumentStatus.Uploading && a.UploadedAt < cutoff)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
