using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Services;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    [Route("api/studio")]
    [ApiController]
    public class StudioController : ControllerBase
    {
        private readonly ILogger<StudioController> _logger;
        private readonly IMessageService _messageService;
        private readonly IStudioService _studioService;
        private readonly IGroupService _groupService;

        public StudioController(
            ILogger<StudioController> logger,
            IMessageService messageService,
            IStudioService studioService,
            IGroupService groupService)
        {
            _logger = logger;
            _messageService = messageService;
            _studioService = studioService;
            _groupService = groupService;
        }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult<ApiResponse<List<StudioResponse>>>> GetUserStudios()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null && bool.TryParse(isAdminClaim, out var adminResult) && adminResult;

            if (isAdmin)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            var result = await _studioService.GetUserStudiosAsync(userId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);
            return Ok(ApiResponse<List<StudioResponse>>.Success(ErrorCodes.SuccessGetData, message, result));
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<ApiResponse<StudioResponse>>> CreateNewStudio([FromBody] CreateStudioRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null && bool.TryParse(isAdminClaim, out var adminResult) && adminResult;

            if (isAdmin)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            var result = await _studioService.CreateStudioAsync(userId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessCreateStudio);
            return Ok(ApiResponse<StudioResponse>.Success(ErrorCodes.SuccessCreateStudio, message, result));
        }

        [HttpGet("{studioId}")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<StudioResponse>>> ViewStudioDetail(Guid studioId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null && bool.TryParse(isAdminClaim, out var adminResult) && adminResult;

            if (isAdmin)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            var result = await _studioService.GetStudioDetailAsync(userId, studioId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);
            return Ok(ApiResponse<StudioResponse>.Success(ErrorCodes.SuccessGetData, message, result));
        }

        [HttpPut]
        [Authorize]
        public async Task<ActionResult<ApiResponse<UpdateStudioResponse>>> UpdateStudio([FromBody] UpdateStudioRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null && bool.TryParse(isAdminClaim, out var adminResult) && adminResult;

            if (isAdmin)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }
            var result = await _studioService.UpdateStudioAsync(userId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessUpdateStudio);
            return Ok(ApiResponse<UpdateStudioResponse>.Success(ErrorCodes.SuccessUpdateStudio, message, result));
        }

        [HttpDelete("{studioId}")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<object>>> DeleteStudio(Guid studioId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null && bool.TryParse(isAdminClaim, out var adminResult) && adminResult;

            if (isAdmin)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            await _studioService.DeleteStudioAsync(userId, studioId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessDeleteStudio);
            return Ok(ApiResponse<object>.Success(ErrorCodes.SuccessDeleteStudio, message, null));
        }

        [HttpGet("{studioId}/groups")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<StudioGroupListResponse>>> ViewStudioGroupList(Guid studioId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null && bool.TryParse(isAdminClaim, out var adminResult) && adminResult;

            if (isAdmin)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }
            var result = new StudioGroupListResponse();
            try
            {
                result = await _groupService.GetStudioGroupsAsync(userId, studioId);
            }
            catch (Exception e)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<StudioGroupListResponse>.Error(ErrorCodes.UnexpectedError, e.Message));
            }
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetGroup);
            return Ok(ApiResponse<StudioGroupListResponse>.Success(ErrorCodes.SuccessGetGroup, message, result));
        }
    }
}
