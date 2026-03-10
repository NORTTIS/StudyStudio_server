using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface ISubscriptionPlanRepository
    {
        Task<List<SubscriptionPlan>> GetAllAsync();
        Task<List<SubscriptionPlan>> GetAllIncludingInactiveAsync();
        Task<SubscriptionPlan?> GetByIdAsync(Guid planId);
        Task UpdateAsync(SubscriptionPlan plan);
    }
}
