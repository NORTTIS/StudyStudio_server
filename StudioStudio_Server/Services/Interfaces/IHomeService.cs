using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IHomeService
    {
        Task<HomeTaskResponse> GetGroupAssignedTaskAsync(Guid userId);
        Task<PersonalTaskStatusResponse> CreateNewGroupTaskStatus(Guid userId, PersonalTaskStatusRequest request);
        Task DeletePersonalTaskStatus(Guid userId, Guid taskStatusId);
        Task UpdatePersonalTaskStatus(Guid userId, Guid taskStatusId, PersonalTaskStatusRequest request);
        Task ReorderPersonalTaskStatus(Guid userId, ReorderPersonalTaskStatusRequest request);
    }
}
