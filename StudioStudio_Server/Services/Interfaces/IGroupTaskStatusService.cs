using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IGroupTaskStatusService
    {
        Task<GroupTaskStatusResponse> CreateNewGroupTaskStatus(Guid userId, Guid groupId, GroupTaskStatusRequest request);
        Task<GroupTaskStatusResponse> GetGroupTaskStatusDetail(Guid taskStatusId);
        Task UpdateGroupTaskStatus(Guid userId, Guid groupId, Guid taskStatusId, GroupTaskStatusRequest request);
        Task SoftDeleteGroupTaskStatus(Guid userId, Guid groupId, Guid taskStatusId);
        Task ReorderGroupTaskStatus(Guid userId, Guid groupId, ReorderGroupTaskStatusRequest request);
    }
}
