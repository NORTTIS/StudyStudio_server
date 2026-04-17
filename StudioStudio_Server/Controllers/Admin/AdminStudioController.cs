using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;
using StudioStudio_Server.Utils;

namespace StudioStudio_Server.Controllers.Admin
{
    /// <summary>
    /// Admin Controller for Studio Management
    /// Route: /api/admin/studios
    /// Only accessible by admin users
    /// </summary>
    [Route("api/admin/studios")]
    [ApiController]
    [Authorize]
    public class AdminStudioController(
        IAdminStudioService adminStudioService,
        IMessageService messageService) : ControllerBase
    {
        /// <summary>
        /// [ADMIN] GET /api/admin/studios
        /// Get paginated list of studios with filters
        /// Query params:
        /// - SearchTerm: Search by studio name
        /// - PageNumber: Page number (default: 1)
        /// - PageSize: Page size (default: 10)
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<AdminStudioListResponse>>> GetStudios([FromQuery] GetStudiosRequest request)
        {
            JwtHelper.ValidateAdminUser(User);

            var response = await adminStudioService.GetStudiosAsync(request);
            var message = messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<AdminStudioListResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [ADMIN] PATCH /api/admin/studios/{id}/status
        /// Update studio status (activate/inactivate)
        /// Body: { "isActive": true | false }
        /// </summary>
        [HttpPatch("{id:guid}/status")]
        public async Task<ActionResult<ApiResponse<string>>> UpdateStudioStatus(
            Guid id,
            [FromBody] UpdateStudioStatusRequest request)
        {
            JwtHelper.ValidateAdminUser(User);

            await adminStudioService.UpdateStudioStatusAsync(id, request.IsActive);

            var message = messageService.GetMessage(ErrorCodes.SuccessUpdateData);

            return Ok(ApiResponse<string>.Success(
                ErrorCodes.SuccessUpdateData,
                message,
                string.Empty));
        }
    }
}
