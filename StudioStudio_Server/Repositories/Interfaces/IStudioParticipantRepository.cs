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
        Task UpdateAsync(StudioParticipant participant);

        Task<List<StudioParticipant>> GetStudiosByUserIdAsync(Guid userId);
        Task<List<StudioParticipant>> GetPendingByStudioIdAsync(Guid studioId);
        Task<bool> IsUserApprovedInStudioAsync(Guid studioId, Guid userId);
        Task<StudioParticipant?> GetPendingByStudioAndUserAsync(Guid studioId, Guid userId);
        Task<StudioParticipant?> GetByStudioAndUserIncludeNonApprovedAsync(Guid studioId, Guid userId);

        /// <summary>
        /// Get participant record by StudioId and UserId (tracked for update/delete)
        /// Condition: StudioId = {studioId} AND UserId = {userId} AND Studio.IsDeleted = false
        /// Use case: Remove member from studio
        /// </summary>
        Task<StudioParticipant?> GetByStudioAndUserTrackedAsync(Guid studioId, Guid userId);
    }
}
