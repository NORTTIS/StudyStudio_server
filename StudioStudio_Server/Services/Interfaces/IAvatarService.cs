using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IAvatarService
    {
        /// <summary>
        /// Request a presigned upload URL for a group avatar
        /// Validates: user is Owner/Moderator of the group, file size, content type
        /// </summary>
        Task<AvatarUploadResponse> RequestGroupAvatarUploadAsync(Guid userId, Guid groupId, RequestAvatarUploadRequest request);

        /// <summary>
        /// Request a presigned upload URL for a studio avatar
        /// Validates: user is Owner of the studio, file size, content type
        /// </summary>
        Task<AvatarUploadResponse> RequestStudioAvatarUploadAsync(Guid userId, Guid studioId, RequestAvatarUploadRequest request);

        /// <summary>
        /// Complete group avatar upload - verify file exists in B2 and update group
        /// </summary>
        Task CompleteGroupAvatarUploadAsync(Guid userId, Guid groupId, CompleteAvatarUploadRequest request);

        /// <summary>
        /// Complete studio avatar upload - verify file exists in B2 and update studio
        /// </summary>
        Task CompleteStudioAvatarUploadAsync(Guid userId, Guid studioId, CompleteAvatarUploadRequest request);

        /// <summary>
        /// Delete a group avatar from B2 and clear the database field
        /// </summary>
        Task DeleteGroupAvatarAsync(Guid userId, Guid groupId);

        /// <summary>
        /// Delete a studio avatar from B2 and clear the database field
        /// </summary>
        Task DeleteStudioAvatarAsync(Guid userId, Guid studioId);

        Task<AvatarUploadResponse> RequestGroupBannerUploadAsync(Guid userId, Guid groupId, RequestAvatarUploadRequest request);
        Task CompleteGroupBannerUploadAsync(Guid userId, Guid groupId, CompleteAvatarUploadRequest request);
        Task DeleteGroupBannerAsync(Guid userId, Guid groupId);
        Task<AvatarUploadResponse> RequestStudioBannerUploadAsync(Guid userId, Guid studioId, RequestAvatarUploadRequest request);
        Task CompleteStudioBannerUploadAsync(Guid userId, Guid studioId, CompleteAvatarUploadRequest request);
        Task DeleteStudioBannerAsync(Guid userId, Guid studioId);
    }
}
