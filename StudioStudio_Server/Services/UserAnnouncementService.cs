using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using System.Diagnostics;

namespace StudioStudio_Server.Services
{
    public class UserAnnouncementService : IUserAnnouncementService
    {
        private readonly IUserAnnouccementRepository _userAnnouccementRepository;
        public UserAnnouncementService(IUserAnnouccementRepository userAnnouccementRepository)
        {
            _userAnnouccementRepository = userAnnouccementRepository;
        }
        public async Task AddAnnouncementAsync(UserAnnouncementRequest request)
        {
            var newAnnouce = new UserAnnouncement
            {
                UserAnnouncementId = Guid.NewGuid(),
                AnnouncementId = request.AnnouncementId,
                CreatedAt = request.CreatedAt,
                MetionedId = request.MentionedId,
                IsRead = request.IsRead,
                IsDelete = false,
                UpdatedAt = request.CreatedAt
            };
            await _userAnnouccementRepository.AddAsync(newAnnouce);
        }

        public async Task RemoveAnnouncementAsync(Guid announceId)
        {
            await _userAnnouccementRepository.DeleteAsync(announceId);
        }
    }
}
