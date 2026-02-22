using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IGroupRepository
    {
        Task<List<Group>> GetUserGroupsAsync(Guid userId);
    }
}
