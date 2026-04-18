using Microsoft.Extensions.Options;
using StudioStudio_Server.Configurations;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    public class AvatarService(
        ILogger<AvatarService> logger,
        IGroupRepository groupRepository,
        IStudioRepository studioRepository,
        IGroupParticipantRepository groupParticipantRepository,
        IStudioParticipantRepository studioParticipantRepository,
        IFileStorageService fileStorageService,
        IOptions<BackblazeConfig> backblazeConfig) : IAvatarService
    {
        private readonly BackblazeConfig _backblazeConfig = backblazeConfig.Value;

        // Avatar upload settings
        private const long MaxAvatarFileSize = 5 * 1024 * 1024; // 5 MB
        private static readonly HashSet<string> AllowedContentTypes = new()
        {
            "image/jpeg",
            "image/png",
            "image/webp",
            "image/gif"
        };

        private static readonly Dictionary<string, string> ContentTypeToExtension = new()
        {
            { "image/jpeg", ".jpg" },
            { "image/png", ".png" },
            { "image/webp", ".webp" },
            { "image/gif", ".gif" }
        };

        /// <summary>
        /// Build full B2 URL for avatar (uses public bucket)
        /// </summary>
        private string BuildAvatarUrl(string fileKey)
        {
            return $"{_backblazeConfig.ServiceUrl.TrimEnd('/')}/{_backblazeConfig.PublicBucketName}/{fileKey}";
        }

        /// <summary>
        /// Extract file key from full URL (for deletion)
        /// </summary>
        private string ExtractFileKey(string? avatarUrl)
        {
            if (string.IsNullOrEmpty(avatarUrl))
                return string.Empty;

            if (!avatarUrl.Contains("://"))
                return avatarUrl;

            var uri = new Uri(avatarUrl);

            var path = uri.AbsolutePath.TrimStart('/');

            // remove bucket prefix if exists
            var bucketPrefix = _backblazeConfig.PublicBucketName + "/";

            if (path.StartsWith(bucketPrefix))
                path = path.Substring(bucketPrefix.Length);

            return path;
        }

        public async Task<AvatarUploadResponse> RequestGroupAvatarUploadAsync(
            Guid userId, Guid groupId, RequestAvatarUploadRequest request)
        {
            // Check if group exists (with tracking for avatar URL access)
            var group = await groupRepository.GetByIdAsync(groupId);
            if (group == null)
            {
                throw new AppException(ErrorCodes.GroupNotFound, StatusCodes.Status404NotFound);
            }

            // Check if user is Owner or Moderator of the group
            var participant = await groupParticipantRepository.GetByGroupAndUserAsync(groupId, userId);
            if (participant == null ||
                (participant.Role != GroupRole.Owner && participant.Role != GroupRole.Moderator))
            {
                throw new AppException(ErrorCodes.GroupUpdatePermissionDenied, StatusCodes.Status403Forbidden);
            }

            // Validate file size
            if (request.FileSize <= 0 || request.FileSize > MaxAvatarFileSize)
            {
                throw new AppException(ErrorCodes.ValidationFileSizeExceeded);
            }

            // Validate content type
            if (!AllowedContentTypes.Contains(request.ContentType.ToLowerInvariant()))
            {
                throw new AppException(ErrorCodes.ValidationInvalidFileFormat);
            }

            // Get file extension
            var extension = ContentTypeToExtension.GetValueOrDefault(
                request.ContentType.ToLowerInvariant(), ".jpg");

            // Generate B2 key
            var fileKey = $"avatars/groups/{groupId}/avatar{extension}";

            // Delete old avatar if exists
            if (!string.IsNullOrEmpty(group.AvatarUrl))
            {
                try
                {
                    var oldKey = ExtractFileKey(group.AvatarUrl);
                    if (!string.IsNullOrEmpty(oldKey))
                        await fileStorageService.DeleteFileAsync(oldKey, _backblazeConfig.PublicBucketName);
                    logger.LogInformation("Deleted old avatar for group {GroupId}: {OldKey}", groupId, oldKey);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete old avatar for group {GroupId}", groupId);
                }
            }

            // Generate presigned upload URL (60 minutes) using public bucket
            var uploadUrl = await fileStorageService.GeneratePresignedUploadUrlAsync(fileKey, _backblazeConfig.PublicBucketName, 60);

            logger.LogInformation(
                "Avatar upload requested for group {GroupId} by user {UserId}. Key: {FileKey}",
                groupId, userId, fileKey);

            return new AvatarUploadResponse
            {
                EntityId = groupId,
                UploadUrl = uploadUrl,
                FileKey = fileKey,
                ExpiresIn = 60
            };
        }

        public async Task<AvatarUploadResponse> RequestStudioAvatarUploadAsync(
            Guid userId, Guid studioId, RequestAvatarUploadRequest request)
        {
            // Check if studio exists (with tracking for avatar URL access)
            var studio = await studioRepository.GetByIdAsync(studioId);
            if (studio == null)
            {
                throw new AppException(ErrorCodes.StudioNotFound, StatusCodes.Status404NotFound);
            }

            // Check if user is Owner of the studio
            if (studio.OwnerId != userId)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            // Validate file size
            if (request.FileSize <= 0 || request.FileSize > MaxAvatarFileSize)
            {
                throw new AppException(ErrorCodes.ValidationFileSizeExceeded);
            }

            // Validate content type
            if (!AllowedContentTypes.Contains(request.ContentType.ToLowerInvariant()))
            {
                throw new AppException(ErrorCodes.ValidationInvalidFileFormat);
            }

            // Get file extension
            var extension = ContentTypeToExtension.GetValueOrDefault(
                request.ContentType.ToLowerInvariant(), ".jpg");

            // Generate B2 key
            var fileKey = $"avatars/studios/{studioId}/avatar{extension}";

            // Delete old avatar if exists
            if (!string.IsNullOrEmpty(studio.AvatarUrl))
            {
                try
                {
                    var oldKey = ExtractFileKey(studio.AvatarUrl);
                    if (!string.IsNullOrEmpty(oldKey))
                        await fileStorageService.DeleteFileAsync(oldKey, _backblazeConfig.PublicBucketName);
                    logger.LogInformation("Deleted old avatar for studio {StudioId}: {OldKey}", studioId, oldKey);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete old avatar for studio {StudioId}", studioId);
                }
            }

            // Generate presigned upload URL (60 minutes) using public bucket
            var uploadUrl = await fileStorageService.GeneratePresignedUploadUrlAsync(fileKey, _backblazeConfig.PublicBucketName, 60);

            logger.LogInformation(
                "Avatar upload requested for studio {StudioId} by user {UserId}. Key: {FileKey}",
                studioId, userId, fileKey);

            return new AvatarUploadResponse
            {
                EntityId = studioId,
                UploadUrl = uploadUrl,
                FileKey = fileKey,
                ExpiresIn = 60
            };
        }

        public async Task CompleteGroupAvatarUploadAsync(
            Guid userId, Guid groupId, CompleteAvatarUploadRequest request)
        {
            // Check if group exists (with tracking for update)
            var group = await groupRepository.GetByIdForUpdateAsync(groupId);
            if (group == null)
            {
                throw new AppException(ErrorCodes.GroupNotFound, StatusCodes.Status404NotFound);
            }

            // Check if user is Owner or Moderator
            var participant = await groupParticipantRepository.GetByGroupAndUserAsync(groupId, userId);
            if (participant == null ||
                (participant.Role != GroupRole.Owner && participant.Role != GroupRole.Moderator))
            {
                throw new AppException(ErrorCodes.GroupUpdatePermissionDenied, StatusCodes.Status403Forbidden);
            }

            // Verify file exists in B2
            var fileExists = await fileStorageService.FileExistsAsync(request.FileKey, _backblazeConfig.PublicBucketName);
            if (!fileExists)
            {
                throw new AppException(ErrorCodes.ValidationInvalidFileFormat);
            }

            // Update group with full B2 URL
            group.AvatarUrl = BuildAvatarUrl(request.FileKey);
            group.UpdatedAt = DateTime.UtcNow;
            await groupRepository.UpdateAsync(group);

            logger.LogInformation(
                "Avatar upload completed for group {GroupId}. Key: {FileKey}",
                groupId, request.FileKey);
        }

        public async Task CompleteStudioAvatarUploadAsync(
            Guid userId, Guid studioId, CompleteAvatarUploadRequest request)
        {
            // Check if studio exists (with tracking for update)
            var studio = await studioRepository.GetByIdForUpdateAsync(studioId);
            if (studio == null)
            {
                throw new AppException(ErrorCodes.StudioNotFound, StatusCodes.Status404NotFound);
            }

            // Check if user is Owner
            if (studio.OwnerId != userId)
            {
                var participant = await studioParticipantRepository.GetByStudioAndUserAsync(studioId, userId);
                if (participant == null)
                {
                    throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
                }
            }

            // Verify file exists in B2
            var fileExists = await fileStorageService.FileExistsAsync(request.FileKey, _backblazeConfig.PublicBucketName);
            if (!fileExists)
            {
                throw new AppException(ErrorCodes.ValidationInvalidFileFormat);
            }

            // Update studio with full B2 URL
            studio.AvatarUrl = BuildAvatarUrl(request.FileKey);
            studio.UpdatedAt = DateTime.UtcNow;
            await studioRepository.UpdateStudioAsync(studio);

            logger.LogInformation(
                "Avatar upload completed for studio {StudioId}. Key: {FileKey}",
                studioId, request.FileKey);
        }

        public async Task DeleteGroupAvatarAsync(Guid userId, Guid groupId)
        {
            var group = await groupRepository.GetByIdForUpdateAsync(groupId);
            if (group == null)
            {
                throw new AppException(ErrorCodes.GroupNotFound, StatusCodes.Status404NotFound);
            }

            var participant = await groupParticipantRepository.GetByGroupAndUserAsync(groupId, userId);
            if (participant == null ||
                (participant.Role != GroupRole.Owner && participant.Role != GroupRole.Moderator))
            {
                throw new AppException(ErrorCodes.GroupUpdatePermissionDenied, StatusCodes.Status403Forbidden);
            }

            // Delete from B2 if exists
            if (!string.IsNullOrEmpty(group.AvatarUrl))
            {
                try
                {
                    var fileKey = ExtractFileKey(group.AvatarUrl);
                    if (!string.IsNullOrEmpty(fileKey))
                        await fileStorageService.DeleteFileAsync(fileKey, _backblazeConfig.PublicBucketName);
                    logger.LogInformation("Deleted avatar for group {GroupId}", groupId);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete avatar from B2 for group {GroupId}", groupId);
                }
            }

            // Clear database field
            group.AvatarUrl = null;
            group.UpdatedAt = DateTime.UtcNow;
            await groupRepository.UpdateAsync(group);
        }

        public async Task DeleteStudioAvatarAsync(Guid userId, Guid studioId)
        {
            var studio = await studioRepository.GetByIdForUpdateAsync(studioId);
            if (studio == null)
            {
                throw new AppException(ErrorCodes.StudioNotFound, StatusCodes.Status404NotFound);
            }

            if (studio.OwnerId != userId)
            {
                var participant = await studioParticipantRepository.GetByStudioAndUserAsync(studioId, userId);
                if (participant == null)
                {
                    throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
                }
            }

            // Delete from B2 if exists
            if (!string.IsNullOrEmpty(studio.AvatarUrl))
            {
                try
                {
                    var fileKey = ExtractFileKey(studio.AvatarUrl);
                    if (!string.IsNullOrEmpty(fileKey))
                        await fileStorageService.DeleteFileAsync(fileKey, _backblazeConfig.PublicBucketName);
                    logger.LogInformation("Deleted avatar for studio {StudioId}", studioId);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete avatar from B2 for studio {StudioId}", studioId);
                }
            }

            // Clear database field
            studio.AvatarUrl = null;
            studio.UpdatedAt = DateTime.UtcNow;
            await studioRepository.UpdateStudioAsync(studio);
        }

        // 🔹 ADDED: Group Banner Methods
        private const long MaxBannerFileSize = 10 * 1024 * 1024; // 10MB

        private async Task<AvatarUploadResponse> GenerateBannerUploadAsync(
            string currentUrl, RequestAvatarUploadRequest request, string fileKey)
        {
            if (request.FileSize <= 0 || request.FileSize > MaxBannerFileSize)
                throw new AppException(ErrorCodes.ValidationFileSizeExceeded);

            if (!AllowedContentTypes.Contains(request.ContentType.ToLowerInvariant()))
                throw new AppException(ErrorCodes.ValidationInvalidFileFormat);

            if (!string.IsNullOrEmpty(currentUrl))
            {
                try
                {
                    var oldKey = ExtractFileKey(currentUrl);
                    if (!string.IsNullOrEmpty(oldKey))
                        await fileStorageService.DeleteFileAsync(oldKey, _backblazeConfig.PublicBucketName);
                }
                catch (Exception ex) { logger.LogWarning(ex, "Failed to delete old banner"); }
            }

            var uploadUrl = await fileStorageService.GeneratePresignedUploadUrlAsync(fileKey, _backblazeConfig.PublicBucketName, 60);
            return new AvatarUploadResponse { EntityId = Guid.NewGuid(), UploadUrl = uploadUrl, FileKey = fileKey, ExpiresIn = 60 };
        }

        public async Task<AvatarUploadResponse> RequestGroupBannerUploadAsync(
            Guid userId, Guid groupId, RequestAvatarUploadRequest request)
        {
            var group = await groupRepository.GetByIdAsync(groupId);
            if (group == null) throw new AppException(ErrorCodes.GroupNotFound, StatusCodes.Status404NotFound);

            var participant = await groupParticipantRepository.GetByGroupAndUserAsync(groupId, userId);
            if (participant == null || (participant.Role != GroupRole.Owner && participant.Role != GroupRole.Moderator))
                throw new AppException(ErrorCodes.GroupUpdatePermissionDenied, StatusCodes.Status403Forbidden);

            var extension = ContentTypeToExtension.GetValueOrDefault(request.ContentType.ToLowerInvariant(), ".jpg");
            var fileKey = $"banners/groups/{groupId}/banner{extension}";
            return await GenerateBannerUploadAsync(group.BannerUrl ?? "", request, fileKey);
        }

        public async Task CompleteGroupBannerUploadAsync(
            Guid userId, Guid groupId, CompleteAvatarUploadRequest request)
        {
            var group = await groupRepository.GetByIdForUpdateAsync(groupId);
            if (group == null) throw new AppException(ErrorCodes.GroupNotFound, StatusCodes.Status404NotFound);

            var participant = await groupParticipantRepository.GetByGroupAndUserAsync(groupId, userId);
            if (participant == null || (participant.Role != GroupRole.Owner && participant.Role != GroupRole.Moderator))
                throw new AppException(ErrorCodes.GroupUpdatePermissionDenied, StatusCodes.Status403Forbidden);

            var fileExists = await fileStorageService.FileExistsAsync(request.FileKey, _backblazeConfig.PublicBucketName);
            if (!fileExists) throw new AppException(ErrorCodes.ValidationInvalidFileFormat);

            group.BannerUrl = BuildAvatarUrl(request.FileKey);
            group.UpdatedAt = DateTime.UtcNow;
            await groupRepository.UpdateAsync(group);
        }

        public async Task DeleteGroupBannerAsync(Guid userId, Guid groupId)
        {
            var group = await groupRepository.GetByIdForUpdateAsync(groupId);
            if (group == null) throw new AppException(ErrorCodes.GroupNotFound, StatusCodes.Status404NotFound);

            var participant = await groupParticipantRepository.GetByGroupAndUserAsync(groupId, userId);
            if (participant == null || (participant.Role != GroupRole.Owner && participant.Role != GroupRole.Moderator))
                throw new AppException(ErrorCodes.GroupUpdatePermissionDenied, StatusCodes.Status403Forbidden);

            if (!string.IsNullOrEmpty(group.BannerUrl))
            {
                try
                {
                    var fileKey = ExtractFileKey(group.BannerUrl);
                    if (!string.IsNullOrEmpty(fileKey))
                        await fileStorageService.DeleteFileAsync(fileKey, _backblazeConfig.PublicBucketName);
                }
                catch (Exception ex) { logger.LogWarning(ex, "Failed to delete banner from B2"); }
            }
            group.BannerUrl = null;
            group.UpdatedAt = DateTime.UtcNow;
            await groupRepository.UpdateAsync(group);
        }

        // 🔹 ADDED: Studio Banner Methods
        public async Task<AvatarUploadResponse> RequestStudioBannerUploadAsync(
            Guid userId, Guid studioId, RequestAvatarUploadRequest request)
        {
            var studio = await studioRepository.GetByIdAsync(studioId);
            if (studio == null) throw new AppException(ErrorCodes.StudioNotFound, StatusCodes.Status404NotFound);
            if (studio.OwnerId != userId) throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);

            var extension = ContentTypeToExtension.GetValueOrDefault(request.ContentType.ToLowerInvariant(), ".jpg");
            var fileKey = $"banners/studios/{studioId}/banner{extension}";
            return await GenerateBannerUploadAsync(studio.BannerUrl ?? "", request, fileKey);
        }

        public async Task CompleteStudioBannerUploadAsync(
            Guid userId, Guid studioId, CompleteAvatarUploadRequest request)
        {
            var studio = await studioRepository.GetByIdForUpdateAsync(studioId);
            if (studio == null) throw new AppException(ErrorCodes.StudioNotFound, StatusCodes.Status404NotFound);
            if (studio.OwnerId != userId) throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);

            var fileExists = await fileStorageService.FileExistsAsync(request.FileKey, _backblazeConfig.PublicBucketName);
            if (!fileExists) throw new AppException(ErrorCodes.ValidationInvalidFileFormat);

            studio.BannerUrl = BuildAvatarUrl(request.FileKey);
            studio.UpdatedAt = DateTime.UtcNow;
            await studioRepository.UpdateStudioAsync(studio);
        }

        public async Task DeleteStudioBannerAsync(Guid userId, Guid studioId)
        {
            var studio = await studioRepository.GetByIdForUpdateAsync(studioId);
            if (studio == null) throw new AppException(ErrorCodes.StudioNotFound, StatusCodes.Status404NotFound);
            if (studio.OwnerId != userId) throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);

            if (!string.IsNullOrEmpty(studio.BannerUrl))
            {
                try
                {
                    var fileKey = ExtractFileKey(studio.BannerUrl);
                    if (!string.IsNullOrEmpty(fileKey))
                        await fileStorageService.DeleteFileAsync(fileKey, _backblazeConfig.PublicBucketName);
                }
                catch (Exception ex) { logger.LogWarning(ex, "Failed to delete banner from B2"); }
            }
            studio.BannerUrl = null;
            studio.UpdatedAt = DateTime.UtcNow;
            await studioRepository.UpdateStudioAsync(studio);
        }
    }
}
