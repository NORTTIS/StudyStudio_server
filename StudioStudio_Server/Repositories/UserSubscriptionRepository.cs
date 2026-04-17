using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository xử lý các thao tác với UserSubscription entity
    /// </summary>
    public class UserSubscriptionRepository(StudioDbContext db) : IUserSubscriptionRepository
    {
        private readonly StudioDbContext _db = db;

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

        public async Task<UserSubscription?> GetActiveSubscriptionAsync(Guid userId)
        {
            DateTime now = DateTime.UtcNow;
            return await _db.UserSubscriptions
                .FirstOrDefaultAsync(us => us.UserId == userId && us.IsActive && us.EndDate > now);
        }

        public async Task DeactivateActiveSubscriptionsAsync(Guid userId)
        {
            var activeSubscriptions = await _db.UserSubscriptions
                .Where(us => us.UserId == userId && us.IsActive)
                .ToListAsync();

            foreach (var sub in activeSubscriptions)
                sub.IsActive = false;

            await _db.SaveChangesAsync();
        }

        public async Task AddAsync(UserSubscription subscription)
        {
            await _db.UserSubscriptions.AddAsync(subscription);
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Get subscriber count for each plan
        /// Returns dictionary of PlanId -> SubscriberCount
        /// Only counts active subscriptions (IsActive = true AND EndDate > Now)
        /// </summary>
        public async Task<Dictionary<Guid, int>> GetSubscriberCountsByPlanAsync()
        {
            DateTime now = DateTime.UtcNow;

            return await _db.UserSubscriptions
                .Where(us => us.IsActive && us.EndDate > now)
                .GroupBy(us => us.PlanId)
                .Select(g => new { PlanId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PlanId, x => x.Count);
        }

        /// <summary>
        /// Count total premium users (users who have ever subscribed to a paid plan)
        /// Includes: Current premium users + Expired premium users
        /// Excludes: Free Plan users (BillingCycle = Free)
        /// Each user is counted only once (Distinct by UserId)
        /// </summary>
        public async Task<int> CountPremiumUsersAsync()
        {
            return await _db.UserSubscriptions
                .Include(us => us.Plan)
                .Where(us => us.Plan.BillingCycle != BillingCycle.Free)
                .Select(us => us.UserId)
                .Distinct()
                .CountAsync();
        }
    }
}
