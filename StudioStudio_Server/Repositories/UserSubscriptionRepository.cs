using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository xử lý các thao tác với UserSubscription entity
    /// </summary>
    public class UserSubscriptionRepository : IUserSubscriptionRepository
    {
        private readonly StudioDbContext _db;

        public UserSubscriptionRepository(StudioDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Lấy subscription plan của user
        /// Điều kiện: UserId = {userId}
        /// Select: Plan info (MaxMembersPerGroup, MaxGroupsPerUser, etc.)
        /// Use case: Check limits khi tạo group hoặc add member
        /// </summary>
        public async Task<SubscriptionPlan?> GetSubscriptionPlanByUserIdAsync(Guid userId)
        {
            return await _db.UserSubscriptions
                .Where(us => us.UserId == userId)
                .Select(us => us.Plan)
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }
    }
}
