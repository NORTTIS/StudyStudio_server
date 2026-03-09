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
            Console.WriteLine($"User {userId} active subscription: {activePlan?.ToString() ?? "None"}");

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
            Console.WriteLine($"User {userId} has no active subscription, returning Free Plan: {freePlan?.ToString() ?? "None"}");

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
    }
}
