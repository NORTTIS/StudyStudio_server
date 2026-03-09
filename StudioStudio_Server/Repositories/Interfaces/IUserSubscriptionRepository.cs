using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IUserSubscriptionRepository
    {
        Task<SubscriptionPlan?> GetSubscriptionPlanByUserIdAsync(Guid userId);
        Task<UserSubscription?> GetActiveSubscriptionAsync(Guid userId);
        Task DeactivateActiveSubscriptionsAsync(Guid userId);
        Task AddAsync(UserSubscription subscription);
    }
}
