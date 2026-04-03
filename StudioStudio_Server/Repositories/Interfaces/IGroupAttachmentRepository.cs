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

        /// <summary>
        /// Hard-delete an attachment record permanently (bypass soft-delete)
        /// </summary>
        Task HardDeleteAsync(Guid attachmentId);

        /// <summary>
        /// Hard-delete multiple attachment records permanently
        /// </summary>
        Task HardDeleteManyAsync(List<Guid> attachmentIds);

        /// <summary>
        /// Decrement group storage used after document deletion
        /// </summary>
        Task DecrementStorageUsedAsync(Guid groupId, long fileSize);

        /// <summary>
        /// Get stuck uploads (Uploading status older than threshold)
        /// </summary>
        Task<List<GroupAttachment>> GetStuckUploadsAsync(TimeSpan olderThan);
    }
}
