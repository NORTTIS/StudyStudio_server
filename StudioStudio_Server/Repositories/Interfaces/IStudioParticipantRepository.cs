using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IStudioParticipantRepository
    {
        Task AddAsync(StudioParticipant participant);
        Task<bool> IsUserInStudioAsync(Guid studioId, Guid userId);
        Task<StudioParticipant?> GetByStudioAndUserAsync(Guid studioId, Guid userId);
        Task<int> GetParticipantCountByStudioIdAsync(Guid studioId);
        Task<List<StudioParticipant>> GetParticipantsByStudioIdAsync(Guid studioId);
    }
}
