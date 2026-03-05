using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Repositories.Interfaces
{
    /// <summary>
    /// Repository interface cho Group Attachments/Documents
    /// </summary>
    public interface IGroupAttachmentRepository
    {
        Task<GroupAttachment?> GetByIdAsync(Guid attachmentId);
        Task<List<GroupAttachment>> GetByGroupIdAsync(Guid groupId);
        Task<List<GroupAttachment>> GetByGroupIdWithStatusAsync(Guid groupId, DocumentStatus status);
        Task<int> CountByGroupIdAsync(Guid groupId);
        Task CreateAsync(GroupAttachment attachment);
        Task UpdateAsync(GroupAttachment attachment);
        Task<bool> FileKeyExistsAsync(string fileKey);
        Task<long> GetTotalStorageUsedByGroupAsync(Guid groupId);
    }
}
