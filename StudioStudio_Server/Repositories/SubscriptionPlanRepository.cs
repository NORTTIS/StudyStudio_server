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
    public class SubscriptionPlanRepository : ISubscriptionPlanRepository
    {
        private readonly StudioDbContext _db;

        public SubscriptionPlanRepository(StudioDbContext db)
        {
            _db = db;
        }

        public async Task<List<SubscriptionPlan>> GetAllAsync()
        {
            return await _db.SubscriptionPlans
                .Where(s => s.IsActive)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<SubscriptionPlan>> GetAllIncludingInactiveAsync()
        {
            return await _db.SubscriptionPlans
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<SubscriptionPlan?> GetByIdAsync(Guid planId)
        {
            return await _db.SubscriptionPlans
                .FirstOrDefaultAsync(p => p.PlanId == planId);
        }

        public async Task UpdateAsync(SubscriptionPlan plan)
        {
            _db.SubscriptionPlans.Update(plan);
            await _db.SaveChangesAsync();
        }
    }
}
