using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    /// <summary>
    /// Controller qu?n l? Studios (không gian làm vi?c)
    /// Route: /api/studio
    /// </summary>
    [Route("api/studio")]
    [ApiController]
    [Authorize]
    public class StudioController : ControllerBase
    {
        private readonly IStudioService _studioService;
        private readonly IGroupService _groupService;
        private readonly IMessageService _messageService;

        public StudioController(
            IStudioService studioService,
            IGroupService groupService,
            IMessageService messageService)
        {
            _studioService = studioService;
            _groupService = groupService;
            _messageService = messageService;
        }

        /// <summary>
        /// Xác th?c và l?y userId t? JWT token
        /// Validate: User không ðý?c là admin
        /// </summary>
        private Guid ValidateAndGetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(
                    ErrorCodes.AuthInvalidCredential,
                    StatusCodes.Status401Unauthorized);
            }

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null &&
                          bool.TryParse(isAdminClaim, out var adminResult) &&
                          adminResult;

            if (isAdmin)
            {
                throw new AppException(
                    ErrorCodes.AuthForbidden,
                    StatusCodes.Status403Forbidden);
            }

            return userId;
        }

        /// <summary>
        /// [AUTHORIZED] GET /api/studio
        /// L?y danh sách t?t c? studios c?a user
        /// Ði?u ki?n: OwnerId = userId
        /// S?p x?p: CreatedAt DESC
        /// Bao g?m: GroupCount c?a m?i studio
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<StudioResponse>>>> GetUserStudios()
        {
            var userId = ValidateAndGetUserId();
            var result = await _studioService.GetUserStudiosAsync(userId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<List<StudioResponse>>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        /// <summary>
        /// [AUTHORIZED] GET /api/studio/{studioId}/groups
        /// L?y danh sách groups trong studio
        /// Validate: User ph?i là owner c?a studio
        /// S?p x?p: Groups theo UpdatedAt DESC
        /// Bao g?m: Studio info + danh sách groups
        /// </summary>
        [HttpGet("{studioId}/groups")]
        public async Task<ActionResult<ApiResponse<StudioGroupListResponse>>> ViewStudioGroupList(Guid studioId)
        {
            var userId = ValidateAndGetUserId();
            var result = await _groupService.GetStudioGroupsAsync(userId, studioId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetGroup);

            return Ok(ApiResponse<StudioGroupListResponse>.Success(
                ErrorCodes.SuccessGetGroup,
                message,
                result));
        }

        /// <summary>
        /// [AUTHORIZED] POST /api/studio
        /// T?o m?i m?t studio
        /// Validate: Studio limit theo subscription plan
        /// Auto-set: CreatedAt, UpdatedAt = UtcNow
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<StudioResponse>>> CreateNewStudio(
            [FromBody] CreateStudioRequest request)
        {
            var userId = ValidateAndGetUserId();
            var result = await _studioService.CreateStudioAsync(userId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessCreateStudio);

            return Ok(ApiResponse<StudioResponse>.Success(
                ErrorCodes.SuccessCreateStudio,
                message,
                result));
        }

        /// <summary>
        /// [AUTHORIZED] PUT /api/studio
        /// C?p nh?t thông tin studio
        /// Validate:
        /// - Studio ph?i t?n t?i
        /// - User ph?i là owner c?a studio
        /// Auto-set: UpdatedAt = UtcNow
        /// </summary>
        [HttpPut]
        public async Task<ActionResult<ApiResponse<UpdateStudioResponse>>> UpdateStudio(
            [FromBody] UpdateStudioRequest request)
        {
            var userId = ValidateAndGetUserId();
            var result = await _studioService.UpdateStudioAsync(userId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessUpdateStudio);

            return Ok(ApiResponse<UpdateStudioResponse>.Success(
                ErrorCodes.SuccessUpdateStudio,
                message,
                result));
        }

        /// <summary>
        /// [AUTHORIZED] DELETE /api/studio/{studioId}
        /// Xóa (soft delete) m?t studio
        /// Validate:
        /// - Studio ph?i t?n t?i
        /// - User ph?i là owner c?a studio
        /// Effect: Set IsActive = false (ho?c DeletedFlag = true)
        /// </summary>
        [HttpDelete("{studioId}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteStudio(Guid studioId)
        {
            var userId = ValidateAndGetUserId();
            await _studioService.DeleteStudioAsync(userId, studioId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessDeleteStudio);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessDeleteStudio,
                message,
                null));
        }
    }
}
