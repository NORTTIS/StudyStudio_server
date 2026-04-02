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

        /// <summary>
        /// Get all studio participant records for a user
        /// Condition: UserId = {userId}
        /// Use case: Get all studios where user is a participant (member)
        /// </summary>
        Task<List<StudioParticipant>> GetStudiosByUserIdAsync(Guid userId);

        // 🔹 ADDED: Pending membership & approval methods
        /// <summary>
        /// Get all pending (not yet approved) members of a studio
        /// Condition: StudioId = {studioId} AND IsApproved = false
        /// </summary>
        Task<List<StudioParticipant>> GetPendingByStudioIdAsync(Guid studioId);

        /// <summary>
        /// Check if user is an approved member of a studio
        /// Condition: StudioId+UserId in StudioParticipants AND IsApproved = true AND Studio.IsDeleted = false
        /// </summary>
        Task<bool> IsUserApprovedInStudioAsync(Guid studioId, Guid userId);

        /// <summary>
        /// Get pending participant record for a user in a studio (if any)
        /// Condition: StudioId+UserId in StudioParticipants AND IsApproved = false
        /// </summary>
        Task<StudioParticipant?> GetPendingByStudioAndUserAsync(Guid studioId, Guid userId);
    }
}
