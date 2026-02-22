using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IGroupService
    {
        Task<GroupListResponse> GetGroupsAsync(Guid userId);
    }
}
