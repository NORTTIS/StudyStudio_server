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
        Task<List<GroupAttachment>> GetByGroupIdPagedAsync(Guid groupId, int skip, int take);
        Task<int> CountByGroupIdAsync(Guid groupId);
        Task CreateAsync(GroupAttachment attachment);
        Task UpdateAsync(GroupAttachment attachment);
        Task<long> GetTotalStorageUsedByGroupAsync(Guid groupId);

        /// <summary>
        /// Hard-delete an attachment record permanently (bypass soft-delete)
        /// </summary>
        Task HardDeleteAsync(Guid attachmentId);

        /// <summary>
        /// Hard-delete multiple attachment records permanently
        /// </summary>

        /// <summary>
        /// Get stuck uploads (Uploading status older than threshold)
        /// </summary>
        Task<List<GroupAttachment>> GetStuckUploadsAsync(TimeSpan olderThan);
    }
}
