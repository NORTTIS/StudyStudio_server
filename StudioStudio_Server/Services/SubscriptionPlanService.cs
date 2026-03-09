using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    public class SubscriptionPlanService : ISubscriptionPlanService
    {
        private readonly ISubscriptionPlanRepository _subscriptionPlanRepository;
        public SubscriptionPlanService(ISubscriptionPlanRepository subscriptionPlanRepository)
        {
            _subscriptionPlanRepository = subscriptionPlanRepository;
        }
        public async Task<SubscriptionPlanResponse> GetAllAsync()
        {
            var listPlan = await _subscriptionPlanRepository.GetAllAsync();
            var response = listPlan.Select(p => new SubscriptionPlanItem
            {
                PlanId = p.PlanId,
                PlanName = p.PlanName,
                Price = p.Price,
                BillingCycle = p.BillingCycle,
                Description = p.Description,
                MaxStudios = p.MaxStudios,
                MaxStorageMb = p.MaxStorageMb,
                MaxAiRequestsPerDay = p.MaxAiRequestsPerDay,
                MaxGroups = p.MaxGroups,
                MaxMembersPerGroup = p.MaxMembersPerGroup
            }).ToList();
            return new SubscriptionPlanResponse
            {
                Plans = response,
            };
        }
    }
}
