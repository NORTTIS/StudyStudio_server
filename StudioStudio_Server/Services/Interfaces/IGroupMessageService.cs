using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    /// <summary>
    /// Service interface cho Group Messages (l?y l?ch s? tin nh?n)
    /// Note: Realtime messaging ðý?c handle b?i GroupDiscussHub (SignalR)
    /// </summary>
    public interface IGroupMessageService
    {
        Task<GroupMessageListResponse> GetGroupMessagesAsync(Guid userId, Guid groupId, int limit, int offset);
    }
}
