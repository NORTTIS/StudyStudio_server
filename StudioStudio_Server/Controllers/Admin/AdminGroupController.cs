using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services;
using StudioStudio_Server.Services.Interfaces;
using StudioStudio_Server.Utils;

namespace StudioStudio_Server.Controllers.Admin
{
    /// <summary>
    /// Admin Controller for Group Management
    /// Route: /api/admin/groups
    /// Only accessible by admin users
    /// </summary>
    [Route("api/admin/groups")]
    [ApiController]
    [Authorize]
    public class AdminGroupController(
        IAdminGroupService adminGroupService,
        IMessageService messageService) : ControllerBase
    {
        /// <summary>
        /// [ADMIN] GET /api/admin/groups
        /// Get paginated list of groups with filters
        /// Query params:
        /// - SearchTerm: Search by group name
        /// - GroupType: Filter by type ("Independent" or "Studio")
        /// - PageNumber: Page number (default: 1)
        /// - PageSize: Page size (default: 10)
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<AdminGroupListResponse>>> GetGroups([FromQuery] GetGroupsRequest request)
        {
            JwtHelper.ValidateAdminUser(User);

            var response = await adminGroupService.GetGroupsAsync(request);
            var message = messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<AdminGroupListResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [ADMIN] PATCH /api/admin/groups/{id}/status
        /// Update group status (activate/inactivate)
        /// Body: { "isActive": true | false }
        /// </summary>
        [HttpPatch("{id:guid}/status")]
        public async Task<ActionResult<ApiResponse<string>>> UpdateGroupStatus(
            Guid id,
            [FromBody] UpdateGroupStatusRequest request)
        {
            JwtHelper.ValidateAdminUser(User);

            await adminGroupService.UpdateGroupStatusAsync(id, request.IsActive);

            var message = messageService.GetMessage(ErrorCodes.SuccessUpdateData);

            return Ok(ApiResponse<string>.Success(
                ErrorCodes.SuccessUpdateData,
                message,
                string.Empty));
        }
    }
}
