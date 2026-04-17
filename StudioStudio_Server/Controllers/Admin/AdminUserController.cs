using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Services.Interfaces;
using StudioStudio_Server.Utils;

namespace StudioStudio_Server.Controllers.Admin
{
    /// <summary>
    /// Admin Controller for User Management
    /// Route: /api/admin/users
    /// Only accessible by admin users
    /// </summary>
    [Route("api/admin/users")]
    [ApiController]
    [Authorize]
    public class AdminUserController(
        IAdminUserService adminUserService,
        IMessageService messageService) : ControllerBase
    {
        /// <summary>
        /// [ADMIN] GET /api/admin/users
        /// Get paginated list of users with filters
        /// Query params:
        /// - SearchTerm: Search by name or email
        /// - Status: Filter by status (Active, Inactive, Deleted)
        /// - Package: Filter by package ("Free" or "Premium")
        /// - PageNumber: Page number (default: 1)
        /// - PageSize: Page size (default: 10)
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<UserListResponse>>> GetUsers([FromQuery] GetUsersRequest request)
        {
            JwtHelper.ValidateAdminUser(User);

            var response = await adminUserService.GetUsersAsync(request);
            var message = messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<UserListResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [ADMIN] GET /api/admin/users/{id}
        /// Get detailed user information by ID
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApiResponse<UserDetailItem>>> GetUserDetail(Guid id)
        {
            JwtHelper.ValidateAdminUser(User);

            var response = await adminUserService.GetUserDetailAsync(id);
            var message = messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<UserDetailItem>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [ADMIN] PATCH /api/admin/users/{id}/status
        /// Update user status (activate/inactivate)
        /// Body: { "status": "Active" | "Inactive" }
        /// </summary>
        [HttpPatch("{id:guid}/status")]
        public async Task<ActionResult<ApiResponse<string>>> UpdateUserStatus(Guid id, [FromBody] UpdateUserStatusRequest request)
        {
            JwtHelper.ValidateAdminUser(User);

            if (!Enum.TryParse<UserStatus>(request.Status, true, out var status))
            {
                throw new AppException(
                    ErrorCodes.UserInvalidStatus);
            }
            await adminUserService.UpdateUserStatusAsync(id, status);

            var message = messageService.GetMessage(ErrorCodes.SuccessUpdateData);

            return Ok(ApiResponse<string>.Success(
                ErrorCodes.SuccessUpdateData,
                message,
                string.Empty));
        }
    }
}
