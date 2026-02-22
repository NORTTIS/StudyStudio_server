using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    [Route("api/group")]
    [ApiController]
    public class GroupController : ControllerBase
    {
        private readonly ILogger<GroupController> _logger;
        private readonly IMessageService _messageService;
        private readonly IGroupService _groupService;

        public GroupController(ILogger<GroupController> logger, IMessageService messageService, IGroupService groupService)
        {
            _logger = logger;
            _messageService = messageService;
            _groupService = groupService;
        }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult<ApiResponse<GroupListResponse>>> GetGroups()
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
            var result = new GroupListResponse();
            try
            {
                result = await _groupService.GetGroupsAsync(userId);
            }
            catch (Exception e)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<GroupListResponse>.Error(ErrorCodes.UnexpectedError, e.Message));
            }
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetGroup);
            return Ok(ApiResponse<GroupListResponse>.Success(ErrorCodes.SuccessGetGroup, message, result));
        }
    }
}
