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
        Task RemoveAsync(StudioParticipant participant);

        /// <summary>
        /// Get all studio participant records for a user
        /// Condition: UserId = {userId}
        /// Use case: Get all studios where user is a participant (member)
        /// </summary>
        Task<List<StudioParticipant>> GetStudiosByUserIdAsync(Guid userId);
    }
}
