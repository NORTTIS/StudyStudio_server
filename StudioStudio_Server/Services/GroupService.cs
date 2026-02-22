using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    public class GroupService : IGroupService
    {
        private readonly ILogger<GroupService> _logger;
        private readonly IMessageService _messageService;
        private readonly IGroupRepository _groupRepository;
        private readonly IUserSubscriptionRepository _userSubscriptionRepository;
        private readonly IFavouriteRepository _favouriteRepository;
        private readonly IUserRepository _userRepository;
        private readonly IStudioRepository _studioRepository;
        private readonly IGroupParticipantRepository _groupParticipantRepository;
        private readonly ITaskRepository _taskRepository;

        public GroupService(
            ILogger<GroupService> logger,
            IMessageService messageService,
            IGroupRepository groupRepository,
            IUserSubscriptionRepository userSubscriptionRepository,
            IFavouriteRepository favouriteRepository,
            IUserRepository userRepository,
            IStudioRepository studioRepository,
            IGroupParticipantRepository groupParticipantRepository,
            ITaskRepository taskRepository)
        {
            _logger = logger;
            _messageService = messageService;
            _groupRepository = groupRepository;
            _userSubscriptionRepository = userSubscriptionRepository;
            _favouriteRepository = favouriteRepository;
            _userRepository = userRepository;
            _studioRepository = studioRepository;
            _groupParticipantRepository = groupParticipantRepository;
            _taskRepository = taskRepository;
        }

        public async Task<GroupListResponse> GetGroupsAsync(Guid userId)
        {
            // Get user's subscription plan
            var subscriptionPlan = await _userSubscriptionRepository.GetSubscriptionPlanByUserIdAsync(userId);

            // Get all groups user participates in
            var groups = await _groupRepository.GetUserGroupsAsync(userId);

            // Get all group IDs
            var groupIds = groups.Select(g => g.GroupId).ToList();

            // Get favorites for this user
            var favorites = await _favouriteRepository.GetByUserAndGroupIdsAsync(userId, groupIds);
            var favoriteGroupIds = favorites.Select(f => f.GroupId).ToHashSet();

            // Get all creators
            var creatorIds = groups.Select(g => g.CreatedBy).Distinct().ToList();
            var creators = await _userRepository.GetByIdsAsync(creatorIds);

            // Get studios
            var studioIds = groups.Where(g => g.StudioId.HasValue)
                .Select(g => g.StudioId.Value).Distinct().ToList();
            var studios = await _studioRepository.GetByIdsAsync(studioIds);

            // Get all participants for member previews
            var allParticipants = await _groupParticipantRepository.GetByGroupIdsAsync(groupIds);

            var participantUserIds = allParticipants.Select(gp => gp.UserId).Distinct().ToList();
            var users = await _userRepository.GetByIdsAsync(participantUserIds);

            // Get task counts
            var taskCounts = await _taskRepository.GetTaskCountByGroupIdsAsync(groupIds);

            // Build GroupCardDto for all groups
            var groupCards = groups.Select(g =>
            {
                var userRole = g.Participants.FirstOrDefault(p => p.UserId == userId)?.Role ?? GroupRole.Viewer;
                var createdByUser = creators.FirstOrDefault(u => u.UserId == g.CreatedBy);
                var studio = g.StudioId.HasValue ? studios.FirstOrDefault(s => s.StudioId == g.StudioId) : null;

                var groupParticipants = allParticipants.Where(gp => gp.GroupId == g.GroupId).ToList();

                var membersPreview = groupParticipants
                    .Take(5)
                    .Select(gp =>
                    {
                        var user = users.FirstOrDefault(u => u.UserId == gp.UserId);
                        return new MemberPreviewDto
                        {
                            Id = gp.UserId,
                            FirstName = user?.FirstName ?? "",
                            LastName = user?.LastName ?? "",
                            AvatarUrl = user?.AvatarUrl
                        };
                    })
                    .ToList();

                return new GroupCardDto
                {
                    Id = g.GroupId,
                    Name = g.GroupName,
                    Description = g.Description ?? "",
                    IsFavorite = favoriteGroupIds.Contains(g.GroupId),
                    Role = userRole.ToString(),
                    Studio = studio != null ? new StudioDto
                    {
                        Id = studio.StudioId,
                        Name = studio.StudioName
                    } : null,
                    CreatedBy = createdByUser != null ? new UserDto
                    {
                        Id = createdByUser.UserId,
                        FirstName = createdByUser.FirstName,
                        LastName = createdByUser.LastName,
                        AvatarUrl = createdByUser.AvatarUrl
                    } : new UserDto(),
                    MemberCount = groupParticipants.Count,
                    TaskCount = taskCounts.TryGetValue(g.GroupId, out var count) ? count : 0,
                    LastActivityAt = g.UpdatedAt,
                    MembersPreview = membersPreview
                };
            }).ToList();

            // Categorize groups
            var favoriteGroups = groupCards.Where(g => g.IsFavorite).ToList();
            var studioGroups = groupCards.Where(g => g.Studio != null).ToList();
            var independentGroups = groupCards.Where(g => g.Studio == null).ToList();

            // Count groups created by user (where user is Owner)
            var groupsCreatedByUser = groups.Count(g =>
                g.Participants.Any(p => p.Role == GroupRole.Owner && p.UserId == userId));

            var response = new GroupListResponse
            {
                Subscription = new SubscriptionInfo
                {
                    GroupLimit = subscriptionPlan?.MaxGroups ?? 5,
                    GroupCreated = groupsCreatedByUser,
                    MemberLimit = subscriptionPlan?.MaxMembersPerGroup ?? 10
                },
                Summary = new GroupSummary
                {
                    TotalGroups = groups.Count,
                    FavoriteCount = favoriteGroups.Count,
                    StudioGroupCount = studioGroups.Count,
                    IndependentGroupCount = independentGroups.Count
                },
                Sections = new GroupSections
                {
                    Favorites = favoriteGroups,
                    StudioGroups = studioGroups,
                    IndependentGroups = independentGroups
                }
            };

            return response;
        }
    }
}
