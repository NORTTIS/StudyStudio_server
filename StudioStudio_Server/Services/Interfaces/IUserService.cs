using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IUserService
    {
        Task<User?> GetByIdAsync(Guid userId);
        Task UpdateAsync(User user);
        Task DeleteAsync(Guid userId);
        Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
        Task UpdateProfileAsync(Guid userId, UpdateUserProfileRequest request);
        Task<(int usedToday, int dailyLimit)> GetAiRequestLimitInfoAsync(Guid userId);
        Task<SubscriptionPlanItem> GetUserSubscriptionPlan(Guid userId);
    }
}
