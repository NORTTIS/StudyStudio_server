using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(Guid id);
        Task<User?> GetByIdIncludingDeletedAsync(Guid id);
        Task<User?> GetByEmailAsync(string email);
        Task AddAsync(User user);
        Task UpdateAsync(User user);
        Task<List<User>> GetByIdsAsync(List<Guid> userIds);
        Task<int> CountActiveUsersAsync();

        /// <summary>
        /// Get paginated users with filters for admin dashboard
        /// </summary>
        Task<(List<User> Users, int TotalCount)> GetUsersAsync(
            string? searchTerm,
            UserStatus? status,
            string? package,
            int pageNumber,
            int pageSize);

        /// <summary>
        /// Get user by ID with all related data for admin detail view
        /// </summary>
        Task<User?> GetByIdWithDetailsAsync(Guid userId);

        /// <summary>
        /// Get studio counts for a list of users (avoids N+1)
        /// </summary>
        Task<Dictionary<Guid, int>> GetStudioCountsAsync(List<Guid> userIds);

        /// <summary>
        /// Get user summary statistics for admin dashboard
        /// </summary>
        Task<(int TotalUsers, int ActiveUsers, int InactiveUsers, int DeletedUsers, int PremiumUsers, int FreeUsers)> GetUserSummaryAsync();

        /// <summary>
        /// Update user status (activate/inactivate)
        /// </summary>
        Task UpdateUserStatusAsync(Guid userId, UserStatus status);
    }
}
