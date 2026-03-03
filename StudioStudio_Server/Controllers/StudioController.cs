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
    /// Controller for managing Studios
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
        /// Authenticate and get userId from JWT token
        /// Validate: User must not be admin (admin cannot use user APIs)
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
        /// Get list of studios owned by user
        /// Condition: OwnerId = userId
        /// Order by: CreatedAt DESC
        /// Include: GroupCount for each studio
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
        /// Get list of groups in studio
        /// Validate: User must be owner of studio
        /// Order by: Groups by UpdatedAt DESC
        /// Include: Studio info + list of groups
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
        /// Create new studio
        /// Validate: Studio limit according to subscription plan
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
        /// Update studio information
        /// Validate:
        /// - Studio must exist
        /// - User must be owner of studio
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
        /// Delete (soft delete) a studio
        /// Validate:
        /// - Studio must exist
        /// - User must be owner of studio
        /// Effect: Set IsActive = false (or DeletedFlag = true)
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
