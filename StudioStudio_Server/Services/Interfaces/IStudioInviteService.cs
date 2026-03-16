using StudioStudio_Server.Models.Caches;

namespace StudioStudio_Server.Services.Interfaces
{
    /// <summary>
    /// Service interface for Studio Invite (mời thành viên vào studio)
    /// </summary>
    public interface IStudioInviteService
    {
        Task<string> GenerateInviteTokenAsync();
        Task<bool> StoreInviteTokenAsync(string token, StudioInviteToken inviteData);
        Task<StudioInviteToken?> GetInviteTokenDataAsync(string token);
        Task<bool> CheckInviteCreationRateLimitAsync(Guid studioId, Guid userId);
    }
}
