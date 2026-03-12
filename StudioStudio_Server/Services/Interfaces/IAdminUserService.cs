using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IAdminUserService
    {
        /// <summary>
        /// Get paginated list of users with filters
        /// </summary>
        Task<UserListResponse> GetUsersAsync(GetUsersRequest request);

        /// <summary>
        /// Get detailed user information by ID
        /// </summary>
        Task<UserDetailItem> GetUserDetailAsync(Guid userId);

        /// <summary>
        /// Update user status (activate/inactivate)
        /// </summary>
        Task UpdateUserStatusAsync(Guid userId, UserStatus status);
    }
}
