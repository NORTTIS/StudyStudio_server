using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    public class UserSubscriptionRepository : IUserSubscriptionRepository
    {
        private readonly StudioDbContext _db;

        public UserSubscriptionRepository(StudioDbContext db)
        {
            _db = db;
        }

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
