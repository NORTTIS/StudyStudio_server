using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IStudioRepository
    {
        Task<List<Studio>> GetByIdsAsync(List<Guid> studioIds);
        Task<Studio?> GetByIdAsync(Guid studioId);
        Task<bool> IsUserStudioOwnerAsync(Guid studioId, Guid userId);
        Task<List<Studio>> GetByOwnerIdAsync(Guid ownerId);
        Task CreateStudioAsync(Studio newStudio);
        Task<int> CountStudioCreatedByUserAsync(Guid ownerId);
        Task DeleteStudioAsync(Studio studio);
        Task UpdateStudioAsync(Studio studio);
        Task<Studio?> GetByIdForUpdateAsync(Guid studioId);
        Task<List<Group>> GetGroupsByStudioIdAsync(Guid studioId);
        Task<bool> IsStudioNameExistByOwnerIdAsync(string studioName, Guid ownerId);
        Task<bool> IsStudioNameExistExcludingStudioAsync(string studioName, Guid ownerId, Guid excludeStudioId);
        Task<(List<Studio> Studios, int TotalCount)> GetStudiosAsync(
            string? searchTerm,
            int pageNumber,
            int pageSize);
        Task<Studio?> GetByIdAdminAsync(Guid studioId);
        /// <summary>
        /// Get summary statistics for studios (raw values, not DTO)
        /// </summary>
        Task<(int TotalStudios, int ActiveStudios, int InactiveStudios, int TotalMembers, int TotalGroups)> GetStudioSummaryAsync();

        /// <summary>
        /// Get member counts for a list of studios (approved members only)
        /// </summary>
        Task<Dictionary<Guid, int>> GetMemberCountsAsync(List<Guid> studioIds);

        /// <summary>
        /// Get group counts for a list of studios
        /// </summary>
        Task<Dictionary<Guid, int>> GetGroupCountsAsync(List<Guid> studioIds);

        /// <summary>
        /// Get task counts for a list of studios (via groups in studio)
        /// </summary>
        Task<Dictionary<Guid, int>> GetTaskCountsAsync(List<Guid> studioIds);

        /// <summary>
        /// Get last activity for a list of studios
        /// Last activity = MAX(Studio.UpdatedAt, MAX(Group.UpdatedAt), MAX(Task.UpdatedAt), MAX(GroupMessage.CreatedAt))
        /// </summary>
        Task<Dictionary<Guid, DateTime?>> GetLastActivityAsync(List<Guid> studioIds);

        /// <summary>
        /// Get owner info (name + email) for a list of owner IDs
        /// </summary>
        Task<Dictionary<Guid, (string Name, string Email)>> GetOwnerInfosAsync(List<Guid> ownerIds);
    }
}
