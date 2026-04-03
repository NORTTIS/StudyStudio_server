using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service xử lý các thao tác admin với studio
    /// </summary>
    public class AdminStudioService : IAdminStudioService
    {
        private readonly IStudioRepository _studioRepository;

        public AdminStudioService(IStudioRepository studioRepository)
        {
            _studioRepository = studioRepository;
        }

        /// <summary>
        /// Get paginated list of studios with filters for admin
        /// </summary>
        public async Task<AdminStudioListResponse> GetStudiosAsync(GetStudiosRequest request)
        {
            // Get studios with pagination
            var (studios, totalCount) = await _studioRepository.GetStudiosAsync(
                request.SearchTerm,
                request.PageNumber,
                request.PageSize);

            // Get summary
            var (totalStudios, activeStudios, inactiveStudios, totalMembers, totalGroups) =
                await _studioRepository.GetStudioSummaryAsync();

            // Get additional data for mapping
            var studioIds = studios.Select(s => s.StudioId).ToList();
            var memberCounts = await _studioRepository.GetMemberCountsAsync(studioIds);
            var groupCounts = await _studioRepository.GetGroupCountsAsync(studioIds);
            var taskCounts = await _studioRepository.GetTaskCountsAsync(studioIds);
            var lastActivities = await _studioRepository.GetLastActivityAsync(studioIds);
            var ownerInfos = await _studioRepository.GetOwnerInfosAsync(studios.Select(s => s.OwnerId).ToList());

            // Map to response DTOs
            var studioList = studios.Select(s => new StudioListItem
            {
                StudioId = s.StudioId,
                StudioName = s.StudioName,
                Description = s.Description,
                OwnerName = ownerInfos.GetValueOrDefault(s.OwnerId).Name ?? "Unknown",
                OwnerEmail = ownerInfos.GetValueOrDefault(s.OwnerId).Email ?? "-",
                GroupCount = groupCounts.GetValueOrDefault(s.StudioId),
                MemberCount = memberCounts.GetValueOrDefault(s.StudioId),
                TaskCount = taskCounts.GetValueOrDefault(s.StudioId),
                CreatedAt = s.CreatedAt,
                LastActivityAt = lastActivities.GetValueOrDefault(s.StudioId),
                IsActive = !s.IsDeleted
            }).ToList();

            return new AdminStudioListResponse
            {
                Summary = new StudioListSummary
                {
                    TotalStudios = totalStudios,
                    ActiveStudios = activeStudios,
                    InactiveStudios = inactiveStudios,
                    TotalMembers = totalMembers,
                    TotalGroups = totalGroups
                },
                StudioList = studioList,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                TotalCount = totalCount
            };
        }

        /// <summary>
        /// Update studio status (activate/inactivate)
        /// </summary>
        public async Task UpdateStudioStatusAsync(Guid studioId, bool isActive)
        {
            var studio = await _studioRepository.GetByIdAdminAsync(studioId);

            if (studio == null)
            {
                throw new AppException(ErrorCodes.StudioNotFound, StatusCodes.Status404NotFound);
            }

            studio.IsDeleted = !isActive;
            studio.UpdatedAt = DateTime.UtcNow;

            await _studioRepository.UpdateStudioAsync(studio);
        }
    }
}
