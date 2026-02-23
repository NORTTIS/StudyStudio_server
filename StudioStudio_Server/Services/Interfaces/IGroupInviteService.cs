using StudioStudio_Server.Models;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IGroupInviteService
    {
        Task<string> GenerateInviteTokenAsync();
        Task<bool> StoreInviteTokenAsync(string token, GroupInviteToken inviteData);
        Task<GroupInviteToken?> GetInviteTokenDataAsync(string token);
        Task<bool> CheckInviteCreationRateLimitAsync(Guid groupId, Guid userId);
    }
}
