using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface ISubscriptionPlanService
    {
        Task<SubscriptionPlanResponse> GetAllAsync();
        Task<SubscriptionStatisticsResponse> GetStatisticsAsync();
        Task<SubscriptionPlanDetail> UpdatePlanAsync(UpdateSubscriptionPlanRequest request);
    }
}
