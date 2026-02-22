using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IUserSubscriptionRepository
    {
        Task<SubscriptionPlan?> GetSubscriptionPlanByUserIdAsync(Guid userId);
    }
}
