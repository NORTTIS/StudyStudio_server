using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository handling CRUD operations with User entity
    /// </summary>
    public class UserRepository(StudioDbContext context) : IUserRepository
    {
        private readonly StudioDbContext _context = context;

        /// <summary>
        /// Add new user to database
        /// </summary>
        public async Task AddAsync(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Get user by email
        /// Condition: Email = {email} AND Status != Deleted
        /// Include: RefreshToken
        /// </summary>
        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _context.Users
                .Include(u => u.RefreshTokens)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == email && u.Status != UserStatus.Deleted);
        }

        /// <summary>
        /// Get user by ID
        /// Condition: UserId = {id} AND Status != Deleted
        /// Include: RefreshToken
        /// </summary>
        public async Task<User?> GetByIdAsync(Guid id)
        {
            return await _context.Users
                .Include(u => u.RefreshTokens)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == id && u.Status != UserStatus.Deleted);
        }

        /// <summary>
        /// Get user by ID including deleted users
        /// Condition: UserId = {id} (no status filter)
        /// Include: RefreshToken
        /// Use case: Check if user exists for deletion, admin operations
        /// </summary>
        public async Task<User?> GetByIdIncludingDeletedAsync(Guid id)
        {
            return await _context.Users
                .Include(u => u.RefreshTokens)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == id);
        }

        /// <summary>
        /// Update user information
        /// </summary>
        public async Task UpdateAsync(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Get multiple users by list of IDs
        /// Condition: UserId IN {userIds} AND Status != Deleted
        /// Use case: Load user info for group members, mentions, etc.
        /// </summary>
        public async Task<List<User>> GetByIdsAsync(List<Guid> userIds)
        {
            return await _context.Users
                .Where(u => userIds.Contains(u.UserId) && u.Status != UserStatus.Deleted)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Count total active users
        /// Condition: Status != Deleted AND Status = Active
        /// Use case: Admin statistics
        /// </summary>
        public async Task<int> CountActiveUsersAsync()
        {
            return await _context.Users
                .Where(u => u.Status != UserStatus.Deleted && u.Status == UserStatus.Active)
                .CountAsync();
        }

        /// <summary>
        /// Get paginated users with filters for admin dashboard
        /// Includes: GroupParticipants (for group count),
        /// UserSubscriptions with Plan (for subscription info), RefreshTokens (for last login)
        /// Note: Studio count is retrieved via separate query to avoid N+1
        /// </summary>
        public async Task<(List<User> Users, int TotalCount)> GetUsersAsync(
            string? searchTerm,
            UserStatus? status,
            string? package,
            int pageNumber,
            int pageSize)
        {
            var query = _context.Users
                .Include(u => u.GroupParticipants)
                .Include(u => u.UserSubscriptions)
                    .ThenInclude(us => us.Plan)
                .Include(u => u.RefreshTokens)
                .Where(u => !u.IsAdmin)  // Exclude admin users
                .AsQueryable();

            // Apply search filter (name or email)
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var searchLower = searchTerm.ToLower();
                query = query.Where(u =>
                    (u.FirstName + " " + u.LastName).ToLower().Contains(searchLower) ||
                    u.Email.ToLower().Contains(searchLower));
            }

            // Apply status filter
            if (status.HasValue)
            {
                query = query.Where(u => u.Status == status.Value);
            }

            // Get total count before pagination
            var totalCount = await query.CountAsync();

            // Apply package filter (Free vs Premium)
            if (!string.IsNullOrWhiteSpace(package))
            {
                var packageLower = package.ToLower();
                query = query.Where(u =>
                    packageLower == "premium"
                        ? u.UserSubscriptions.Any(us =>
                            us.Plan.BillingCycle > 0 &&
                            us.IsActive &&
                            us.EndDate > DateTime.UtcNow)
                        : !u.UserSubscriptions.Any(us =>
                            us.Plan.BillingCycle > 0 &&
                            us.IsActive &&
                            us.EndDate > DateTime.UtcNow));
            }

            // Apply pagination and sorting (newest first)
            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync();

            return (users, totalCount);
        }

        /// <summary>
        /// Get studio count for a list of users (avoids N+1)
        /// </summary>
        public async Task<Dictionary<Guid, int>> GetStudioCountsAsync(List<Guid> userIds)
        {
            if (!userIds.Any())
                return new Dictionary<Guid, int>();

            var studioCounts = await _context.Studios
                .Where(s => userIds.Contains(s.OwnerId) && !s.IsDeleted)
                .GroupBy(s => s.OwnerId)
                .Select(g => new { OwnerId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.OwnerId, x => x.Count);

            return studioCounts;
        }

        /// <summary>
        /// Get user by ID with all related data for admin detail view
        /// Includes: GroupParticipants, UserSubscriptions with Plan, RefreshTokens
        /// Note: Studio count is retrieved via separate query as there's no navigation property from User
        /// </summary>
        public async Task<User?> GetByIdWithDetailsAsync(Guid userId)
        {
            return await _context.Users
                .Include(u => u.GroupParticipants)
                .Include(u => u.UserSubscriptions)
                    .ThenInclude(us => us.Plan)
                .Include(u => u.RefreshTokens)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId);
        }

        /// <summary>
        /// Get user summary statistics for admin dashboard
        /// </summary>
        public async Task<(int TotalUsers, int ActiveUsers, int InactiveUsers, int DeletedUsers, int PremiumUsers, int FreeUsers)> GetUserSummaryAsync()
        {
            var allUsers = await _context.Users
                .Where(u => !u.IsAdmin)
                .AsNoTracking()
                .ToListAsync();

            var totalUsers = allUsers.Count;
            var activeUsers = allUsers.Count(u => u.Status == UserStatus.Active);
            var inactiveUsers = allUsers.Count(u => u.Status == UserStatus.Inactive);
            var deletedUsers = allUsers.Count(u => u.Status == UserStatus.Deleted);

            // Get premium users (users with active non-free subscription)
            var premiumUserIds = await _context.UserSubscriptions
                .Where(us => us.Plan.BillingCycle > 0 && us.IsActive && us.EndDate > DateTime.UtcNow)
                .Select(us => us.UserId)
                .Distinct()
                .ToListAsync();

            // Get non-admin user IDs for premium calculation
            var nonAdminUserIds = allUsers.Select(u => u.UserId).ToHashSet();
            var premiumUsers = premiumUserIds.Count(id => nonAdminUserIds.Contains(id));
            var freeUsers = totalUsers - premiumUsers;

            return (totalUsers, activeUsers, inactiveUsers, deletedUsers, premiumUsers, freeUsers);
        }

        /// <summary>
        /// Update user status (activate/inactivate)
        /// </summary>
        public async Task UpdateUserStatusAsync(Guid userId, UserStatus status)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.Status = status;
                user.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }
    }
}
