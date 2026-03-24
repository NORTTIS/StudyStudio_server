using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Controllers
{
    [ApiController]
    [Route("api/avatar")]
    [Authorize]
    public class AvatarController : ControllerBase
    {
        private readonly IAvatarService _avatarService;

        public AvatarController(IAvatarService avatarService)
        {
            _avatarService = avatarService;
        }

        private Guid GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                throw new AppException(ErrorCodes.ValidationInvalidToken, StatusCodes.Status401Unauthorized);
            }
            return userId;
        }

        /// <summary>
        /// Request a presigned upload URL for group avatar
        /// Auth: Owner or Moderator of the group
        /// </summary>
        [HttpPost("group/{groupId}/request-upload")]
        public async Task<IActionResult> RequestGroupAvatarUpload(
            Guid groupId,
            [FromBody] RequestAvatarUploadRequest request,
            CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            var result = await _avatarService.RequestGroupAvatarUploadAsync(userId, groupId, request);
            return Ok(ApiResponse<AvatarUploadResponse>.Success(
                ErrorCodes.SuccessGetData,
                "Presigned upload URL generated successfully",
                result));
        }

        /// <summary>
        /// Request a presigned upload URL for studio avatar
        /// Auth: Owner or Admin of the studio
        /// </summary>
        [HttpPost("studio/{studioId}/request-upload")]
        public async Task<IActionResult> RequestStudioAvatarUpload(
            Guid studioId,
            [FromBody] RequestAvatarUploadRequest request,
            CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            var result = await _avatarService.RequestStudioAvatarUploadAsync(userId, studioId, request);
            return Ok(ApiResponse<AvatarUploadResponse>.Success(
                ErrorCodes.SuccessGetData,
                "Presigned upload URL generated successfully",
                result));
        }

        /// <summary>
        /// Complete group avatar upload
        /// Auth: Owner or Moderator of the group
        /// </summary>
        [HttpPost("group/{groupId}/complete")]
        public async Task<IActionResult> CompleteGroupAvatarUpload(
            Guid groupId,
            [FromBody] CompleteAvatarUploadRequest request,
            CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            await _avatarService.CompleteGroupAvatarUploadAsync(userId, groupId, request);
            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessUpdateData,
                "Group avatar updated successfully",
                new { groupId }));
        }

        /// <summary>
        /// Complete studio avatar upload
        /// Auth: Owner or Admin of the studio
        /// </summary>
        [HttpPost("studio/{studioId}/complete")]
        public async Task<IActionResult> CompleteStudioAvatarUpload(
            Guid studioId,
            [FromBody] CompleteAvatarUploadRequest request,
            CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            await _avatarService.CompleteStudioAvatarUploadAsync(userId, studioId, request);
            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessUpdateData,
                "Studio avatar updated successfully",
                new { studioId }));
        }

        /// <summary>
        /// Delete group avatar
        /// Auth: Owner or Moderator of the group
        /// </summary>
        [HttpDelete("group/{groupId}")]
        public async Task<IActionResult> DeleteGroupAvatar(
            Guid groupId,
            CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            await _avatarService.DeleteGroupAvatarAsync(userId, groupId);
            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessUpdateData,
                "Group avatar deleted successfully",
                new { groupId }));
        }

        /// <summary>
        /// Delete studio avatar
        /// Auth: Owner or Admin of the studio
        /// </summary>
        [HttpDelete("studio/{studioId}")]
        public async Task<IActionResult> DeleteStudioAvatar(
            Guid studioId,
            CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            await _avatarService.DeleteStudioAvatarAsync(userId, studioId);
            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessUpdateData,
                "Studio avatar deleted successfully",
                new { studioId }));
        }
    }
}
