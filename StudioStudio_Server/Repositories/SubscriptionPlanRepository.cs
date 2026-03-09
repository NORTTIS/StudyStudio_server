using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository handling operations with SubscriptionPlan entity
    /// Manages subscription tiers (Free, Premium)
    /// Note: This repository is currently minimal as plan management is handled elsewhere
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
    }
}
