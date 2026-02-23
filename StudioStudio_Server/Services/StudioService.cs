using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    public class StudioService : IStudioService
    {
        private readonly IStudioRepository _studioRepository;
        private readonly IGroupRepository _groupRepository;

        public StudioService(
            IStudioRepository studioRepository,
            IGroupRepository groupRepository)
        {
            _studioRepository = studioRepository;
            _groupRepository = groupRepository;
        }

        public async Task<List<StudioResponse>> GetUserStudiosAsync(Guid userId)
        {
            var studios = await _studioRepository.GetByOwnerIdAsync(userId);

            if (!studios.Any())
            {
                return new List<StudioResponse>();
            }

            var studioResponses = studios.Select(studio => new StudioResponse
            {
                StudioId = studio.StudioId,
                StudioName = studio.StudioName,
                Description = studio.Description,
                OwnerId = studio.OwnerId,
                CreatedAt = studio.CreatedAt,
                UpdatedAt = studio.UpdatedAt,
                GroupCount = 0
            }).ToList();

            foreach (var response in studioResponses)
            {
                response.GroupCount = await _groupRepository.GetGroupCountByStudioIdAsync(response.StudioId);
            }

            return studioResponses;
        }
    }
}
