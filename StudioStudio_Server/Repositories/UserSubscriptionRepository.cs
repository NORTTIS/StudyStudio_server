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
        /// Điều kiện: 
        /// - UserId = {userId} AND IsActive = true AND EndDate > Now
        /// - Nếu không có active subscription → trả về Free Plan (BillingCycle = Free)
        /// 
        /// Select: Plan info (MaxMembersPerGroup, MaxGroups, MaxStorageMb, etc.)
        /// Use case: Check limits khi tạo group, add member, upload document
        /// </summary>
        public async Task<SubscriptionPlan?> GetSubscriptionPlanByUserIdAsync(Guid userId)
        {
            DateTime now = DateTime.UtcNow;

            // Try to get active paid subscription
            SubscriptionPlan? activePlan = await _db.UserSubscriptions
                .Where(us => us.UserId == userId && 
                            us.IsActive && 
                            us.EndDate > now)
                .Select(us => us.Plan)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            // If user has active subscription, return it
            if (activePlan != null)
            {
                return activePlan;
            }

            // Otherwise, return Free Plan (BillingCycle = Free)
            SubscriptionPlan? freePlan = await _db.SubscriptionPlans
                .Where(sp => sp.BillingCycle == BillingCycle.Free && sp.IsActive)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            return freePlan;
        }
    }
}
