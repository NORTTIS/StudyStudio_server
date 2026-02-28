using StudioStudio_Server.Models.Caches;

namespace StudioStudio_Server.Services.Interfaces
{
    /// <summary>
    /// Service interface cho Group Invite (m?i thành viên vào group)
    /// </summary>
    public interface IGroupInviteService
    {
        Task<string> GenerateInviteTokenAsync();
        Task<bool> StoreInviteTokenAsync(string token, GroupInviteToken inviteData);
        Task<GroupInviteToken?> GetInviteTokenDataAsync(string token);
        Task<bool> CheckInviteCreationRateLimitAsync(Guid groupId, Guid userId);
    }
}
