using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository handling operations with SubscriptionPlan entity
    /// Manages subscription tiers (Free, Premium)
    /// </summary>
    public class SubscriptionPlanRepository(StudioDbContext db) : ISubscriptionPlanRepository
    {
        public async Task<List<SubscriptionPlan>> GetAllAsync()
        {
            return await db.SubscriptionPlans
                .Where(s => s.IsActive)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<SubscriptionPlan>> GetAllIncludingInactiveAsync()
        {
            return await db.SubscriptionPlans
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<SubscriptionPlan?> GetByIdAsync(Guid planId)
        {
            return await db.SubscriptionPlans
                .FirstOrDefaultAsync(p => p.PlanId == planId);
        }

        public async Task UpdateAsync(SubscriptionPlan plan)
        {
            db.SubscriptionPlans.Update(plan);
            await db.SaveChangesAsync();
        }
    }
}
