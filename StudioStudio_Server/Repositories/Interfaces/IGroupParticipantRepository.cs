using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IGroupParticipantRepository
    {
        Task<List<GroupParticipant>> GetByGroupIdsAsync(List<Guid> groupIds);
        Task AddAsync(GroupParticipant participant);
    }
}
