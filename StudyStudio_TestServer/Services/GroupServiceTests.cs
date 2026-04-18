using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services;
using StudioStudio_Server.Services.Interfaces;
using Xunit;

namespace StudioStudio_Server.Tests.Services
{
    /// <summary>
    /// Unit tests cho GroupService.
    /// Tests: group CRUD, member management, favorites, analytics.
    /// Ref: Services/GroupService.cs
    /// </summary>
    public class GroupServiceTests
    {
        #region Setup & Helpers

        private readonly Mock<ILogger<GroupService>> _loggerMock;
        private readonly Mock<IMessageService> _messageServiceMock;
        private readonly Mock<IGroupRepository> _groupRepoMock;
        private readonly Mock<IUserSubscriptionRepository> _userSubscriptionRepoMock;
        private readonly Mock<IFavouriteRepository> _favouriteRepoMock;
        private readonly Mock<IUserRepository> _userRepoMock;
        private readonly Mock<IStudioRepository> _studioRepoMock;
        private readonly Mock<IGroupParticipantRepository> _groupParticipantRepoMock;
        private readonly Mock<ITaskRepository> _taskRepoMock;
        private readonly Mock<ITemplateRepository> _templateRepoMock;
        private readonly Mock<IGroupTaskStatusRepository> _groupTaskStatusRepoMock;
        private readonly Mock<ITaskAssignmentRepository> _taskAssignmentRepoMock;
        private readonly Mock<IStudioParticipantRepository> _studioParticipantRepoMock;
        private readonly Mock<IEmailService> _emailServiceMock;
        private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<ICacheService> _cacheServiceMock;

        public GroupServiceTests()
        {
            _loggerMock = new Mock<ILogger<GroupService>>();
            _messageServiceMock = new Mock<IMessageService>();
            _groupRepoMock = new Mock<IGroupRepository>();
            _userSubscriptionRepoMock = new Mock<IUserSubscriptionRepository>();
            _favouriteRepoMock = new Mock<IFavouriteRepository>();
            _userRepoMock = new Mock<IUserRepository>();
            _studioRepoMock = new Mock<IStudioRepository>();
            _groupParticipantRepoMock = new Mock<IGroupParticipantRepository>();
            _taskRepoMock = new Mock<ITaskRepository>();
            _templateRepoMock = new Mock<ITemplateRepository>();
            _groupTaskStatusRepoMock = new Mock<IGroupTaskStatusRepository>();
            _taskAssignmentRepoMock = new Mock<ITaskAssignmentRepository>();
            _studioParticipantRepoMock = new Mock<IStudioParticipantRepository>();
            _emailServiceMock = new Mock<IEmailService>();
            _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
            _configurationMock = new Mock<IConfiguration>();
            _cacheServiceMock = new Mock<ICacheService>();
        }

        private GroupService CreateService()
        {
            return new GroupService(
                _loggerMock.Object,
                _groupRepoMock.Object,
                _userSubscriptionRepoMock.Object,
                _favouriteRepoMock.Object,
                _userRepoMock.Object,
                _studioRepoMock.Object,
                _groupParticipantRepoMock.Object,
                _taskRepoMock.Object,
                _templateRepoMock.Object,
                _groupTaskStatusRepoMock.Object,
                _taskAssignmentRepoMock.Object,
                _studioParticipantRepoMock.Object,
                _emailServiceMock.Object,
                _httpContextAccessorMock.Object,
                _configurationMock.Object,
                _cacheServiceMock.Object);
        }

        private Guid _userId = Guid.NewGuid();
        private Guid _groupId = Guid.NewGuid();
        private Guid _studioId = Guid.NewGuid();

        private User CreateUser(string firstName = "Test", string lastName = "User", string email = "test@example.com", string language = "vi")
            => new()
            {
                UserId = _userId,
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                Language = language,
                Status = UserStatus.Active,
                IsAdmin = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

        private Group CreateGroup(string name = "Test Group", bool isActive = true, bool isArchived = false, bool isOpen = true)
            => new()
            {
                GroupId = _groupId,
                GroupName = name,
                Description = "Test Description",
                CreatedBy = _userId,
                StudioId = null,
                IsTemplate = false,
                IsActive = isActive,
                IsOpen = isOpen,
                IsArchived = isArchived,
                AllowMemberUpdateProgress = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Participants = new List<GroupParticipant>
                {
                    new()
                    {
                        ParticipantId = Guid.NewGuid(),
                        GroupId = _groupId,
                        UserId = _userId,
                        Role = GroupRole.Owner,
                        IsApproved = true,
                        CreatedAt = DateTime.UtcNow
                    }
                }
            };

        private GroupParticipant CreateParticipant(Guid userId, GroupRole role, bool isApproved = true)
            => new()
            {
                ParticipantId = Guid.NewGuid(),
                GroupId = _groupId,
                UserId = userId,
                Role = role,
                IsApproved = isApproved,
                CreatedAt = DateTime.UtcNow
            };

        private Studio CreateStudio(string name = "Test Studio")
            => new()
            {
                StudioId = _studioId,
                StudioName = name,
                OwnerId = _userId,
                IsDeleted = false,
                IsOpen = true,
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

        private Template CreateTemplate(Guid groupId, Guid userId, bool isSystem = false, bool isActive = true)
            => new()
            {
                TemplateId = Guid.NewGuid(),
                UserId = userId,
                GroupId = groupId,
                IsSystemTemplate = isSystem,
                IsActive = isActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

        private SubscriptionPlan CreateSubscriptionPlan(int maxGroups = 5, int maxMembers = 10)
            => new()
            {
                PlanId = Guid.NewGuid(),
                PlanName = "Premium",
                Price = 100000m,
                BillingCycle = BillingCycle.Monthly,
                Description = "Premium Plan",
                MaxStudios = 5,
                MaxStorageMb = 5000,
                MaxAiRequestsPerDay = 100,
                MaxGroups = maxGroups,
                MaxMembersPerGroup = maxMembers,
                IsActive = true
            };

        private void SetupDefaultMocks()
        {
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);
            _configurationMock.Setup(x => x["Frontend:BaseUrl"]).Returns("http://localhost:3000");
        }

        #endregion

        #region GetGroupDetailAsync

        [Fact]
        public async Task GetGroupDetailAsync_GroupNotFound_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            _groupRepoMock.Setup(x => x.GetGroupWithDetailsAsync(_groupId)).ReturnsAsync((Group?)null);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.GetGroupDetailAsync(_userId, _groupId));
            Assert.Equal(ErrorCodes.GroupNotFound, ex.Code);
            Assert.Equal(StatusCodes.Status404NotFound, ex.HttpStatus);
        }

        [Fact]
        public async Task GetGroupDetailAsync_UserNotApprovedMember_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var group = CreateGroup();
            group.Participants.Clear();
            group.Participants.Add(CreateParticipant(Guid.NewGuid(), GroupRole.Owner, true)); // different user

            _groupRepoMock.Setup(x => x.GetGroupWithDetailsAsync(_groupId)).ReturnsAsync(group);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.GetGroupDetailAsync(_userId, _groupId));
            Assert.Equal(ErrorCodes.GroupAccessDenied, ex.Code);
            Assert.Equal(StatusCodes.Status403Forbidden, ex.HttpStatus);
        }

        [Fact]
        public async Task GetGroupDetailAsync_UserPendingMember_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var group = CreateGroup();
            group.Participants.Clear();
            group.Participants.Add(CreateParticipant(_userId, GroupRole.Member, false)); // not approved

            _groupRepoMock.Setup(x => x.GetGroupWithDetailsAsync(_groupId)).ReturnsAsync(group);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.GetGroupDetailAsync(_userId, _groupId));
            Assert.Equal(ErrorCodes.GroupAccessDenied, ex.Code);
        }

        [Fact]
        public async Task GetGroupDetailAsync_ApprovedMember_ReturnsDetailWithStatus()
        {
            // Arrange
            SetupDefaultMocks();
            var group = CreateGroup();
            var status = new GroupTaskStatus
            {
                StatusId = Guid.NewGuid(),
                GroupId = _groupId,
                StatusName = "To Do",
                Position = 0
            };

            _groupRepoMock.Setup(x => x.GetGroupWithDetailsAsync(_groupId)).ReturnsAsync(group);
            _groupParticipantRepoMock.Setup(x => x.GetParticipantCountByGroupIdAsync(_groupId)).ReturnsAsync(1);
            _taskRepoMock.Setup(x => x.GetTaskCountByGroupIdAsync(_groupId)).ReturnsAsync(5);
            _groupTaskStatusRepoMock.Setup(x => x.GetByGroupIdAsync(_groupId)).ReturnsAsync(new List<GroupTaskStatus> { status });
            _taskRepoMock.Setup(x => x.GetListTasksByListStatusId(It.IsAny<List<Guid>>()))
                .ReturnsAsync(new Dictionary<Guid, List<TaskItem>>());
            _favouriteRepoMock.Setup(x => x.IsFavouriteAsync(_userId, _groupId)).ReturnsAsync(true);
            _templateRepoMock.Setup(x => x.GetByGroupIdAsync(_groupId)).ReturnsAsync((Template?)null);
            var service = CreateService();

            // Act
            var result = await service.GetGroupDetailAsync(_userId, _groupId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(_groupId, result.GroupId);
            Assert.Equal("Test Group", result.GroupName);
            Assert.True(result.IsFavorite);
            Assert.Single(result.TaskStatuses);
        }

        [Fact]
        public async Task GetGroupDetailAsync_WithTemplate_SetsIsTemplateTrue()
        {
            // Arrange
            SetupDefaultMocks();
            var group = CreateGroup();
            var template = CreateTemplate(_groupId, _userId, isActive: true);

            _groupRepoMock.Setup(x => x.GetGroupWithDetailsAsync(_groupId)).ReturnsAsync(group);
            _groupParticipantRepoMock.Setup(x => x.GetParticipantCountByGroupIdAsync(_groupId)).ReturnsAsync(1);
            _taskRepoMock.Setup(x => x.GetTaskCountByGroupIdAsync(_groupId)).ReturnsAsync(0);
            _groupTaskStatusRepoMock.Setup(x => x.GetByGroupIdAsync(_groupId)).ReturnsAsync(new List<GroupTaskStatus>());
            _taskRepoMock.Setup(x => x.GetListTasksByListStatusId(It.IsAny<List<Guid>>()))
                .ReturnsAsync(new Dictionary<Guid, List<TaskItem>>());
            _favouriteRepoMock.Setup(x => x.IsFavouriteAsync(_userId, _groupId)).ReturnsAsync(false);
            _templateRepoMock.Setup(x => x.GetByGroupIdAsync(_groupId)).ReturnsAsync(template);
            var service = CreateService();

            // Act
            var result = await service.GetGroupDetailAsync(_userId, _groupId);

            // Assert
            Assert.True(result.IsTemplate);
            Assert.Equal(template.TemplateId, result.TemplateId);
        }

        #endregion

        #region GetGroupMembersAsync

        [Fact]
        public async Task GetGroupMembersAsync_GroupNotFound_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync((Group?)null);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.GetGroupMembersAsync(_userId, _groupId));
            Assert.Equal(ErrorCodes.GroupNotFound, ex.Code);
        }

        [Fact]
        public async Task GetGroupMembersAsync_UserNotApprovedMember_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var group = CreateGroup();
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync(group);
            _groupParticipantRepoMock.Setup(x => x.IsUserApprovedInGroupAsync(_groupId, _userId)).ReturnsAsync(false);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.GetGroupMembersAsync(_userId, _groupId));
            Assert.Equal(ErrorCodes.GroupPermissionDenied, ex.Code);
            Assert.Equal(StatusCodes.Status403Forbidden, ex.HttpStatus);
        }

        [Fact]
        public async Task GetGroupMembersAsync_ApprovedMember_ReturnsMemberList()
        {
            // Arrange
            SetupDefaultMocks();
            var group = CreateGroup();
            var memberId = Guid.NewGuid();
            var participants = new List<GroupParticipant>
            {
                CreateParticipant(_userId, GroupRole.Owner, true),
                CreateParticipant(memberId, GroupRole.Member, true)
            };

            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync(group);
            _groupParticipantRepoMock.Setup(x => x.IsUserApprovedInGroupAsync(_groupId, _userId)).ReturnsAsync(true);
            _groupParticipantRepoMock.Setup(x => x.GetAllByGroupIdAsync(_groupId)).ReturnsAsync(participants);
            _userRepoMock.Setup(x => x.GetByIdsAsync(It.IsAny<List<Guid>>()))
                .ReturnsAsync(new List<User>
                {
                    CreateUser("Owner", "User", "owner@test.com"),
                    CreateUser("Member", "User", "member@test.com")
                });
            var service = CreateService();

            // Act
            var result = await service.GetGroupMembersAsync(_userId, _groupId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.TotalMembers);
            Assert.Equal(2, result.Members.Count);
            Assert.Equal("Owner", result.Members[0].FirstName);
        }

        #endregion

        #region CreateGroupAsync

        [Fact]
        public async Task CreateGroupAsync_GroupLimitReached_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var request = new CreateGroupRequest { GroupName = "New Group", IsOpen = true };
            _userSubscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync(CreateSubscriptionPlan(maxGroups: 5));
            _groupRepoMock.Setup(x => x.CountGroupsCreatedByUserAsync(_userId)).ReturnsAsync(5);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.CreateGroupAsync(_userId, request));
            Assert.Equal(ErrorCodes.GroupLimitReached, ex.Code);
        }

        [Fact]
        public async Task CreateGroupAsync_GroupNameTooLong_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var request = new CreateGroupRequest
            {
                GroupName = new string('A', 256),
                IsOpen = true
            };
            _userSubscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync((SubscriptionPlan?)null);
            _groupRepoMock.Setup(x => x.CountGroupsCreatedByUserAsync(_userId)).ReturnsAsync(0);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.CreateGroupAsync(_userId, request));
            Assert.Equal(ErrorCodes.GroupNameInvalid, ex.Code);
        }

        [Fact]
        public async Task CreateGroupAsync_DescriptionTooLong_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var request = new CreateGroupRequest
            {
                GroupName = "Valid Name",
                Description = new string('A', 501),
                IsOpen = true
            };
            _userSubscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync((SubscriptionPlan?)null);
            _groupRepoMock.Setup(x => x.CountGroupsCreatedByUserAsync(_userId)).ReturnsAsync(0);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.CreateGroupAsync(_userId, request));
            Assert.Equal(ErrorCodes.GroupDescriptionInvalid, ex.Code);
        }

        [Fact]
        public async Task CreateGroupAsync_StudioGroup_StudioNotFound_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var studioId = Guid.NewGuid();
            var request = new CreateGroupRequest
            {
                GroupName = "Studio Group",
                StudioId = studioId,
                IsOpen = true
            };
            _userSubscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync((SubscriptionPlan?)null);
            _groupRepoMock.Setup(x => x.CountGroupsCreatedByUserAsync(_userId)).ReturnsAsync(0);
            _studioRepoMock.Setup(x => x.GetByIdAsync(studioId)).ReturnsAsync((Studio?)null);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.CreateGroupAsync(_userId, request));
            Assert.Equal(ErrorCodes.StudioNotFound, ex.Code);
        }

        [Fact]
        public async Task CreateGroupAsync_StudioGroup_UserNotOwner_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var otherUserId = Guid.NewGuid();
            var studio = CreateStudio();
            studio.OwnerId = otherUserId; // different owner

            var request = new CreateGroupRequest
            {
                GroupName = "Studio Group",
                StudioId = _studioId,
                IsOpen = true
            };
            _userSubscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync((SubscriptionPlan?)null);
            _groupRepoMock.Setup(x => x.CountGroupsCreatedByUserAsync(_userId)).ReturnsAsync(0);
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioRepoMock.Setup(x => x.IsUserStudioOwnerAsync(_studioId, _userId)).ReturnsAsync(false);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.CreateGroupAsync(_userId, request));
            Assert.Equal(ErrorCodes.StudioPermissionDenied, ex.Code);
        }

        [Fact]
        public async Task CreateGroupAsync_StudioGroup_DuplicateName_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var studio = CreateStudio();
            var request = new CreateGroupRequest
            {
                GroupName = "Existing Group",
                StudioId = _studioId,
                IsOpen = true
            };
            _userSubscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync((SubscriptionPlan?)null);
            _groupRepoMock.Setup(x => x.CountGroupsCreatedByUserAsync(_userId)).ReturnsAsync(0);
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioRepoMock.Setup(x => x.IsUserStudioOwnerAsync(_studioId, _userId)).ReturnsAsync(true);
            _groupRepoMock.Setup(x => x.GroupNameExistsInStudioAsync(_studioId, "Existing Group", null)).ReturnsAsync(true);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.CreateGroupAsync(_userId, request));
            Assert.Equal(ErrorCodes.GroupNameAlreadyExists, ex.Code);
        }

        [Fact]
        public async Task CreateGroupAsync_IndependentGroup_DuplicateName_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var request = new CreateGroupRequest { GroupName = "My Group", IsOpen = true };
            _userSubscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync((SubscriptionPlan?)null);
            _groupRepoMock.Setup(x => x.CountGroupsCreatedByUserAsync(_userId)).ReturnsAsync(0);
            _groupRepoMock.Setup(x => x.GroupNameExistsInStudioAsync(null, "My Group", _userId)).ReturnsAsync(true);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.CreateGroupAsync(_userId, request));
            Assert.Equal(ErrorCodes.GroupPersonalAlreadyExists, ex.Code);
        }

        [Fact]
        public async Task CreateGroupAsync_InvalidColorHex_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var request = new CreateGroupRequest
            {
                GroupName = "Valid Group",
                ColorHex = "invalid-color",
                IsOpen = true
            };
            _userSubscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync((SubscriptionPlan?)null);
            _groupRepoMock.Setup(x => x.CountGroupsCreatedByUserAsync(_userId)).ReturnsAsync(0);
            _groupRepoMock.Setup(x => x.GroupNameExistsInStudioAsync(null, "Valid Group", _userId)).ReturnsAsync(false);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.CreateGroupAsync(_userId, request));
            Assert.Equal(ErrorCodes.ValidationInvalidColor, ex.Code);
        }

        [Fact]
        public async Task CreateGroupAsync_InvalidAlias_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var request = new CreateGroupRequest
            {
                GroupName = "Valid Group",
                Alias = "invalid@alias!",
                IsOpen = true
            };
            _userSubscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync((SubscriptionPlan?)null);
            _groupRepoMock.Setup(x => x.CountGroupsCreatedByUserAsync(_userId)).ReturnsAsync(0);
            _groupRepoMock.Setup(x => x.GroupNameExistsInStudioAsync(null, "Valid Group", _userId)).ReturnsAsync(false);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.CreateGroupAsync(_userId, request));
            Assert.Equal(ErrorCodes.ValidationInvalidAlias, ex.Code);
        }

        [Fact]
        public async Task CreateGroupAsync_ValidIndependentGroup_CreatesWithOwnerParticipant()
        {
            // Arrange
            SetupDefaultMocks();
            var request = new CreateGroupRequest
            {
                GroupName = "New Independent Group",
                Description = "A test group",
                ColorHex = "#FF5F3D",
                IconEmoji = "🎯",
                IsOpen = true
            };

            _userSubscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync((SubscriptionPlan?)null);
            _groupRepoMock.Setup(x => x.CountGroupsCreatedByUserAsync(_userId)).ReturnsAsync(0);
            _groupRepoMock.Setup(x => x.GroupNameExistsInStudioAsync(null, "New Independent Group", _userId))
                .ReturnsAsync(false);

            Group? capturedGroup = null;
            GroupParticipant? capturedParticipant = null;
            _groupRepoMock.Setup(x => x.AddAsync(It.IsAny<Group>()))
                .Callback<Group>(g => capturedGroup = g)
                .Returns(Task.CompletedTask);
            _groupParticipantRepoMock.Setup(x => x.AddAsync(It.IsAny<GroupParticipant>()))
                .Callback<GroupParticipant>(p => capturedParticipant = p)
                .Returns(Task.CompletedTask);

            var service = CreateService();

            // Act
            var result = await service.CreateGroupAsync(_userId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("New Independent Group", result.GroupName);
            Assert.Equal("Independent", result.GroupType);
            Assert.Null(result.StudioId);

            Assert.NotNull(capturedGroup);
            Assert.Equal("New Independent Group", capturedGroup.GroupName);
            Assert.Equal(_userId, capturedGroup.CreatedBy);

            Assert.NotNull(capturedParticipant);
            Assert.Equal(GroupRole.Owner, capturedParticipant.Role);
            Assert.True(capturedParticipant.IsApproved);
        }

        [Fact]
        public async Task CreateGroupAsync_WithTemplate_CopiesTaskStatuses()
        {
            // Arrange
            SetupDefaultMocks();
            var templateGroupId = Guid.NewGuid();
            var template = CreateTemplate(templateGroupId, _userId, isSystem: true, isActive: true);
            var templateStatuses = new List<GroupTaskStatus>
            {
                new() { StatusId = Guid.NewGuid(), GroupId = templateGroupId, StatusName = "To Do", Position = 0 },
                new() { StatusId = Guid.NewGuid(), GroupId = templateGroupId, StatusName = "Done", Position = 1 }
            };

            var request = new CreateGroupRequest
            {
                GroupName = "Group From Template",
                TemplateId = template.TemplateId,
                IsOpen = true
            };

            _userSubscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync((SubscriptionPlan?)null);
            _groupRepoMock.Setup(x => x.CountGroupsCreatedByUserAsync(_userId)).ReturnsAsync(0);
            _groupRepoMock.Setup(x => x.GroupNameExistsInStudioAsync(null, "Group From Template", _userId))
                .ReturnsAsync(false);
            _templateRepoMock.Setup(x => x.GetByIdAsync(template.TemplateId)).ReturnsAsync(template);
            _groupTaskStatusRepoMock.Setup(x => x.GetByGroupIdAsync(templateGroupId)).ReturnsAsync(templateStatuses);

            List<GroupTaskStatus>? capturedStatuses = null;
            _groupTaskStatusRepoMock.Setup(x => x.AddRangeAsync(It.IsAny<List<GroupTaskStatus>>()))
                .Callback<List<GroupTaskStatus>>(s => capturedStatuses = s)
                .Returns(Task.CompletedTask);

            var service = CreateService();

            // Act
            var result = await service.CreateGroupAsync(_userId, request);

            // Assert
            Assert.NotNull(capturedStatuses);
            Assert.Equal(2, capturedStatuses.Count);
        }

        [Fact]
        public async Task CreateGroupAsync_TemplateNotFound_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var request = new CreateGroupRequest
            {
                GroupName = "Group",
                TemplateId = Guid.NewGuid(),
                IsOpen = true
            };
            _userSubscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync((SubscriptionPlan?)null);
            _groupRepoMock.Setup(x => x.CountGroupsCreatedByUserAsync(_userId)).ReturnsAsync(0);
            _groupRepoMock.Setup(x => x.GroupNameExistsInStudioAsync(null, "Group", _userId)).ReturnsAsync(false);
            _templateRepoMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Template?)null);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.CreateGroupAsync(_userId, request));
            Assert.Equal(ErrorCodes.TemplateNotFound, ex.Code);
        }

        #endregion

        #region DeleteGroupAsync

        [Fact]
        public async Task DeleteGroupAsync_GroupNotFound_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync((Group?)null);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.DeleteGroupAsync(_userId, _groupId));
            Assert.Equal(ErrorCodes.GroupNotFound, ex.Code);
        }

        [Fact]
        public async Task DeleteGroupAsync_UserNotOwner_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var group = CreateGroup();
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync(group);
            _groupRepoMock.Setup(x => x.IsUserGroupOwnerAsync(_groupId, _userId)).ReturnsAsync(false);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.DeleteGroupAsync(_userId, _groupId));
            Assert.Equal(ErrorCodes.GroupPermissionDenied, ex.Code);
        }

        [Fact]
        public async Task DeleteGroupAsync_OwnerSuccess_SoftDeletes()
        {
            // Arrange
            SetupDefaultMocks();
            var group = CreateGroup();
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync(group);
            _groupRepoMock.Setup(x => x.IsUserGroupOwnerAsync(_groupId, _userId)).ReturnsAsync(true);
            _groupRepoMock.Setup(x => x.DeleteAsync(group)).Returns(Task.CompletedTask);
            var service = CreateService();

            // Act & Assert (should not throw)
            await service.DeleteGroupAsync(_userId, _groupId);
            _groupRepoMock.Verify(x => x.DeleteAsync(group), Times.Once);
        }

        #endregion

        #region UpdateGroupAsync

        [Fact]
        public async Task UpdateGroupAsync_GroupNotFound_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var request = new UpdateGroupRequest { GroupId = _groupId, GroupName = "Updated" };
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync((Group?)null);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.UpdateGroupAsync(_userId, request));
            Assert.Equal(ErrorCodes.GroupNotFound, ex.Code);
        }

        [Fact]
        public async Task UpdateGroupAsync_ViewerCannotUpdate_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var group = CreateGroup();
            var viewerId = Guid.NewGuid();
            group.Participants.Clear();
            group.Participants.Add(CreateParticipant(viewerId, GroupRole.Viewer, true));

            var request = new UpdateGroupRequest { GroupId = _groupId, GroupName = "Updated" };
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync(group);
            _groupParticipantRepoMock.Setup(x => x.GetByGroupAndUserAsync(_groupId, viewerId))
                .ReturnsAsync(CreateParticipant(viewerId, GroupRole.Viewer, true));
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.UpdateGroupAsync(viewerId, request));
            Assert.Equal(ErrorCodes.GroupUpdatePermissionDenied, ex.Code);
        }

        [Fact]
        public async Task UpdateGroupAsync_ModeratorCanUpdate_Success()
        {
            // Arrange
            SetupDefaultMocks();
            var moderatorId = Guid.NewGuid();
            var group = CreateGroup();
            group.Participants.Clear();
            group.Participants.Add(CreateParticipant(_userId, GroupRole.Owner, true));
            group.Participants.Add(CreateParticipant(moderatorId, GroupRole.Moderator, true));

            var request = new UpdateGroupRequest
            {
                GroupId = _groupId,
                GroupName = "Updated Group",
                Description = "New description"
            };

            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync(group);
            _groupParticipantRepoMock.Setup(x => x.GetByGroupAndUserAsync(_groupId, moderatorId))
                .ReturnsAsync(CreateParticipant(moderatorId, GroupRole.Moderator, true));
            _groupParticipantRepoMock.Setup(x => x.GetByGroupAndUserAsync(_groupId, _userId))
                .ReturnsAsync(CreateParticipant(_userId, GroupRole.Owner, true));
            _groupRepoMock.Setup(x => x.GroupNameExistsInStudioExcludingGroupAsync(null, "Updated Group", _groupId))
                .ReturnsAsync(false);
            _templateRepoMock.Setup(x => x.GetByGroupIdAsync(_groupId)).ReturnsAsync((Template?)null);
            _groupRepoMock.Setup(x => x.UpdateAsync(It.IsAny<Group>())).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(x => x.InvalidateAIGroupCacheAsync(_groupId)).Returns(Task.CompletedTask);
            var service = CreateService();

            // Act
            var result = await service.UpdateGroupAsync(moderatorId, request);

            // Assert
            Assert.Equal("Updated Group", result.GroupName);
        }

        [Fact]
        public async Task UpdateGroupAsync_DuplicateNameInSameStudio_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var group = CreateGroup();
            var request = new UpdateGroupRequest
            {
                GroupId = _groupId,
                GroupName = "Existing Name"
            };

            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync(group);
            _groupParticipantRepoMock.Setup(x => x.GetByGroupAndUserAsync(_groupId, _userId))
                .ReturnsAsync(CreateParticipant(_userId, GroupRole.Owner, true));
            _groupRepoMock.Setup(x => x.GroupNameExistsInStudioExcludingGroupAsync(null, "Existing Name", _groupId))
                .ReturnsAsync(true);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.UpdateGroupAsync(_userId, request));
            Assert.Equal(ErrorCodes.GroupNameAlreadyExists, ex.Code);
        }

        [Fact]
        public async Task UpdateGroupAsync_CreateTemplate_CreatesTemplate()
        {
            // Arrange
            SetupDefaultMocks();
            var group = CreateGroup();
            var request = new UpdateGroupRequest
            {
                GroupId = _groupId,
                GroupName = "Template Group",
                IsTemplate = true
            };

            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync(group);
            _groupParticipantRepoMock.Setup(x => x.GetByGroupAndUserAsync(_groupId, _userId))
                .ReturnsAsync(CreateParticipant(_userId, GroupRole.Owner, true));
            _groupRepoMock.Setup(x => x.GroupNameExistsInStudioExcludingGroupAsync(null, "Template Group", _groupId))
                .ReturnsAsync(false);
            _templateRepoMock.Setup(x => x.GetByGroupIdAsync(_groupId)).ReturnsAsync((Template?)null);
            _templateRepoMock.Setup(x => x.AddAsync(It.IsAny<Template>())).Returns(Task.CompletedTask);
            _groupRepoMock.Setup(x => x.UpdateAsync(It.IsAny<Group>())).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(x => x.InvalidateAIGroupCacheAsync(_groupId)).Returns(Task.CompletedTask);
            var service = CreateService();

            // Act
            var result = await service.UpdateGroupAsync(_userId, request);

            // Assert
            Assert.True(result.IsTemplate);
            _templateRepoMock.Verify(x => x.AddAsync(It.IsAny<Template>()), Times.Once);
        }

        [Fact]
        public async Task UpdateGroupAsync_DeactivateTemplate_DeactivatesTemplate()
        {
            // Arrange
            SetupDefaultMocks();
            var group = CreateGroup();
            var existingTemplate = CreateTemplate(_groupId, _userId, isActive: true);
            var request = new UpdateGroupRequest
            {
                GroupId = _groupId,
                GroupName = "Group",
                IsTemplate = false
            };

            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync(group);
            _groupParticipantRepoMock.Setup(x => x.GetByGroupAndUserAsync(_groupId, _userId))
                .ReturnsAsync(CreateParticipant(_userId, GroupRole.Owner, true));
            _groupRepoMock.Setup(x => x.GroupNameExistsInStudioExcludingGroupAsync(null, "Group", _groupId))
                .ReturnsAsync(false);
            _templateRepoMock.Setup(x => x.GetByGroupIdAsync(_groupId)).ReturnsAsync(existingTemplate);
            _templateRepoMock.Setup(x => x.DeleteAsync(existingTemplate)).Returns(Task.CompletedTask);
            _groupRepoMock.Setup(x => x.UpdateAsync(It.IsAny<Group>())).Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(x => x.InvalidateAIGroupCacheAsync(_groupId)).Returns(Task.CompletedTask);
            var service = CreateService();

            // Act
            var result = await service.UpdateGroupAsync(_userId, request);

            // Assert
            Assert.False(result.IsTemplate);
            _templateRepoMock.Verify(x => x.DeleteAsync(existingTemplate), Times.Once);
        }

        #endregion

        #region GetStudioGroupsAsync

        [Fact]
        public async Task GetStudioGroupsAsync_StudioNotFound_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync((Studio?)null);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.GetStudioGroupsAsync(_userId, _studioId));
            Assert.Equal(ErrorCodes.StudioNotFound, ex.Code);
        }

        [Fact]
        public async Task GetStudioGroupsAsync_NeitherOwnerNorMember_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var studio = CreateStudio();
            var outsiderId = Guid.NewGuid();
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioParticipantRepoMock.Setup(x => x.GetByStudioAndUserAsync(_studioId, outsiderId))
                .ReturnsAsync((StudioParticipant?)null);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.GetStudioGroupsAsync(outsiderId, _studioId));
            Assert.Equal(ErrorCodes.AuthForbidden, ex.Code);
        }

        [Fact]
        public async Task GetStudioGroupsAsync_OwnerSeesAllGroups()
        {
            // Arrange
            SetupDefaultMocks();
            var studio = CreateStudio();
            var group1 = CreateGroup("Group 1");
            group1.StudioId = _studioId;
            var group2 = CreateGroup("Group 2");
            group2.StudioId = _studioId;

            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioParticipantRepoMock.Setup(x => x.GetByStudioAndUserAsync(_studioId, _userId))
                .ReturnsAsync((StudioParticipant?)null);
            _groupRepoMock.Setup(x => x.GetStudioGroupsAsync(_studioId))
                .ReturnsAsync(new List<Group> { group1, group2 });
            _groupParticipantRepoMock.Setup(x => x.GetByGroupIdsAsync(It.IsAny<List<Guid>>()))
                .ReturnsAsync(new List<GroupParticipant>());
            _userRepoMock.Setup(x => x.GetByIdsAsync(It.IsAny<List<Guid>>()))
                .ReturnsAsync(new List<User>());
            _taskRepoMock.Setup(x => x.GetTaskCountByGroupIdsAsync(It.IsAny<List<Guid>>()))
                .ReturnsAsync(new Dictionary<Guid, int>());
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(CreateUser());
            var service = CreateService();

            // Act
            var result = await service.GetStudioGroupsAsync(_userId, _studioId);

            // Assert
            Assert.Equal(2, result.TotalGroup);
            Assert.Equal(2, result.StudioGroups.Count);
        }

        [Fact]
        public async Task GetStudioGroupsAsync_MemberSeesOnlyParticipatingGroups()
        {
            // Arrange
            SetupDefaultMocks();
            var studio = CreateStudio();
            var memberId = Guid.NewGuid();
            var g1Id = Guid.NewGuid();
            var g2Id = Guid.NewGuid();

            // Create groups with distinct IDs
            var group1 = new Group
            {
                GroupId = g1Id,
                GroupName = "Group 1",
                StudioId = _studioId,
                CreatedBy = _userId,
                IsActive = true,
                IsOpen = true,
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Participants = new List<GroupParticipant>
                {
                    new() { ParticipantId = Guid.NewGuid(), GroupId = g1Id, UserId = memberId, Role = GroupRole.Member, IsApproved = true, CreatedAt = DateTime.UtcNow }
                }
            };
            var group2 = new Group
            {
                GroupId = g2Id,
                GroupName = "Group 2",
                StudioId = _studioId,
                CreatedBy = _userId,
                IsActive = true,
                IsOpen = true,
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Participants = new List<GroupParticipant>()
            };

            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioParticipantRepoMock.Setup(x => x.GetByStudioAndUserAsync(_studioId, memberId))
                .ReturnsAsync(new StudioParticipant
                {
                    ParticipantId = Guid.NewGuid(),
                    StudioId = _studioId,
                    UserId = memberId,
                    Role = StudioRole.Member,
                    IsApproved = true,
                    CreatedAt = DateTime.UtcNow
                });
            _groupRepoMock.Setup(x => x.GetStudioGroupsAsync(_studioId))
                .ReturnsAsync(new List<Group> { group1, group2 });

            // First call (line 993): with [g1Id, g2Id] → member only in g1Id
            // Second call (line 1009): with [g1Id] → return g1Id's participants
            _groupParticipantRepoMock.Setup(x => x.GetByGroupIdsAsync(It.Is<List<Guid>>(l => l.Count == 2 && l.Contains(g1Id) && l.Contains(g2Id))))
                .ReturnsAsync(new List<GroupParticipant>
                {
                    new() { ParticipantId = Guid.NewGuid(), GroupId = g1Id, UserId = memberId, Role = GroupRole.Member, IsApproved = true, CreatedAt = DateTime.UtcNow }
                });
            _groupParticipantRepoMock.Setup(x => x.GetByGroupIdsAsync(It.Is<List<Guid>>(l => l.Count == 1 && l.Contains(g1Id))))
                .ReturnsAsync(new List<GroupParticipant>
                {
                    new() { ParticipantId = Guid.NewGuid(), GroupId = g1Id, UserId = memberId, Role = GroupRole.Member, IsApproved = true, CreatedAt = DateTime.UtcNow }
                });

            _userRepoMock.Setup(x => x.GetByIdsAsync(It.IsAny<List<Guid>>())).ReturnsAsync(new List<User>());
            _taskRepoMock.Setup(x => x.GetTaskCountByGroupIdsAsync(It.IsAny<List<Guid>>())).ReturnsAsync(new Dictionary<Guid, int>());
            _userRepoMock.Setup(x => x.GetByIdAsync(memberId)).ReturnsAsync(CreateUser());
            var service = CreateService();

            // Act
            var result = await service.GetStudioGroupsAsync(memberId, _studioId);

            // Assert
            Assert.Equal(1, result.TotalGroup);
        }

        #endregion

        #region GetGroupTasksAsync

        [Fact]
        public async Task GetGroupTasksAsync_GroupNotFound_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync((Group?)null);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.GetGroupTasksAsync(_userId, _groupId, 1, 10));
            Assert.Equal(ErrorCodes.GroupNotFound, ex.Code);
        }

        [Fact]
        public async Task GetGroupTasksAsync_UserNotApprovedMember_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var group = CreateGroup();
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync(group);
            _groupParticipantRepoMock.Setup(x => x.IsUserApprovedInGroupAsync(_groupId, _userId))
                .ReturnsAsync(false);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.GetGroupTasksAsync(_userId, _groupId, 1, 10));
            Assert.Equal(ErrorCodes.GroupPermissionDenied, ex.Code);
        }

        [Fact]
        public async Task GetGroupTasksAsync_ValidMember_ReturnsPaginatedTasks()
        {
            // Arrange
            SetupDefaultMocks();
            var group = CreateGroup();
            var taskItem = new TaskItem
            {
                TaskId = Guid.NewGuid(),
                GroupId = _groupId,
                Title = "Test Task",
                Priority = TaskPriority.Medium,
                Severity = TaskSeverity.Moderate,
                Position = 0,
                OwnerId = _userId,
                Owner = CreateUser(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync(group);
            _groupParticipantRepoMock.Setup(x => x.IsUserApprovedInGroupAsync(_groupId, _userId))
                .ReturnsAsync(true);
            _groupTaskStatusRepoMock.Setup(x => x.GetByGroupIdAsync(_groupId))
                .ReturnsAsync(new List<GroupTaskStatus>());
            _taskRepoMock.Setup(x => x.GetGroupTasksWithFiltersAsync(
                _groupId, 1, 10,
                It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<TaskPriority?>(), It.IsAny<TaskSeverity?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<bool?>(),
                "createdAt", true,
                It.IsAny<string?>(), It.IsAny<TaskPriority?>(), It.IsAny<TaskSeverity?>()))
                .ReturnsAsync((new List<TaskItem> { taskItem }, 1));
            _taskAssignmentRepoMock.Setup(x => x.GetListAssigneesByListTaskId(It.IsAny<List<Guid>>()))
                .ReturnsAsync(new List<TaskAssignment>());
            _userRepoMock.Setup(x => x.GetByIdsAsync(It.IsAny<List<Guid>>()))
                .ReturnsAsync(new List<User>());
            var service = CreateService();

            // Act
            var result = await service.GetGroupTasksAsync(_userId, _groupId, 1, 10);

            // Assert
            Assert.Equal(_groupId, result.GroupId);
            Assert.Equal(1, result.TotalCount);
            Assert.Single(result.Items);
        }

        [Fact]
        public async Task GetGroupTasksAsync_NegativePageSize_DefaultsTo10()
        {
            // Arrange
            SetupDefaultMocks();
            var group = CreateGroup();
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync(group);
            _groupParticipantRepoMock.Setup(x => x.IsUserApprovedInGroupAsync(_groupId, _userId))
                .ReturnsAsync(true);
            _groupTaskStatusRepoMock.Setup(x => x.GetByGroupIdAsync(_groupId))
                .ReturnsAsync(new List<GroupTaskStatus>());
            _taskRepoMock.Setup(x => x.GetGroupTasksWithFiltersAsync(
                _groupId, 1, 10, It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
                It.IsAny<TaskPriority?>(), It.IsAny<TaskSeverity?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(), It.IsAny<DateTime?>(),
                It.IsAny<string?>(), It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<bool?>(),
                It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<string?>(), It.IsAny<TaskPriority?>(), It.IsAny<TaskSeverity?>()))
                .ReturnsAsync((new List<TaskItem>(), 0));

            var service = CreateService();

            // Act
            var result = await service.GetGroupTasksAsync(_userId, _groupId, -5, -10);

            // Assert
            Assert.Equal(10, result.PageSize);
        }

        #endregion

        #region ToggleIsOpenAsync

        [Fact]
        public async Task ToggleIsOpenAsync_GroupNotFound_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync((Group?)null);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.ToggleIsOpenAsync(_userId, _groupId, false));
            Assert.Equal(ErrorCodes.GroupNotFound, ex.Code);
        }

        [Fact]
        public async Task ToggleIsOpenAsync_MemberCannotToggle_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var group = CreateGroup();
            var memberId = Guid.NewGuid();
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync(group);
            _groupParticipantRepoMock.Setup(x => x.GetByGroupAndUserAsync(_groupId, memberId))
                .ReturnsAsync(CreateParticipant(memberId, GroupRole.Member, true));
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.ToggleIsOpenAsync(memberId, _groupId, false));
            Assert.Equal(ErrorCodes.GroupUpdatePermissionDenied, ex.Code);
        }

        [Fact]
        public async Task ToggleIsOpenAsync_ModeratorCanToggle_Success()
        {
            // Arrange
            SetupDefaultMocks();
            var moderatorId = Guid.NewGuid();
            var group = CreateGroup(isOpen: false);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync(group);
            _groupParticipantRepoMock.Setup(x => x.GetByGroupAndUserAsync(_groupId, moderatorId))
                .ReturnsAsync(CreateParticipant(moderatorId, GroupRole.Moderator, true));
            _groupRepoMock.Setup(x => x.UpdateAsync(It.IsAny<Group>())).Returns(Task.CompletedTask);
            var service = CreateService();

            // Act
            var result = await service.ToggleIsOpenAsync(moderatorId, _groupId, true);

            // Assert
            Assert.True(result.IsOpen);
            Assert.Equal(_groupId, result.Id);
        }

        #endregion

        #region GetPendingMembersAsync

        [Fact]
        public async Task GetPendingMembersAsync_GroupNotFound_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync((Group?)null);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.GetPendingMembersAsync(_userId, _groupId));
            Assert.Equal(ErrorCodes.GroupNotFound, ex.Code);
        }

        [Fact]
        public async Task GetPendingMembersAsync_MemberCannotView_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var group = CreateGroup();
            var memberId = Guid.NewGuid();
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync(group);
            _groupParticipantRepoMock.Setup(x => x.GetByGroupAndUserAsync(_groupId, memberId))
                .ReturnsAsync(CreateParticipant(memberId, GroupRole.Member, true));
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.GetPendingMembersAsync(memberId, _groupId));
            Assert.Equal(ErrorCodes.GroupUpdatePermissionDenied, ex.Code);
        }

        [Fact]
        public async Task GetPendingMembersAsync_OwnerViews_ReturnsPendingList()
        {
            // Arrange
            SetupDefaultMocks();
            var pendingId = Guid.NewGuid();
            var group = CreateGroup();
            var pendingParticipant = CreateParticipant(pendingId, GroupRole.Member, false);

            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync(group);
            _groupParticipantRepoMock.Setup(x => x.GetByGroupAndUserAsync(_groupId, _userId))
                .ReturnsAsync(CreateParticipant(_userId, GroupRole.Owner, true));
            _groupParticipantRepoMock.Setup(x => x.GetPendingByGroupIdAsync(_groupId))
                .ReturnsAsync(new List<GroupParticipant> { pendingParticipant });
            _userRepoMock.Setup(x => x.GetByIdsAsync(It.IsAny<List<Guid>>()))
                .ReturnsAsync(new List<User> { CreateUser("Pending", "User", "pending@test.com") });
            var service = CreateService();

            // Act
            var result = await service.GetPendingMembersAsync(_userId, _groupId);

            // Assert
            Assert.Equal(1, result.TotalPending);
            Assert.Single(result.PendingMembers);
        }

        #endregion

        #region ApproveMemberAsync

        [Fact]
        public async Task ApproveMemberAsync_GroupNotFound_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var targetId = Guid.NewGuid();
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync((Group?)null);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.ApproveMemberAsync(_userId, _groupId, targetId));
            Assert.Equal(ErrorCodes.GroupNotFound, ex.Code);
        }

        [Fact]
        public async Task ApproveMemberAsync_MemberCannotApprove_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var group = CreateGroup();
            var memberId = Guid.NewGuid();
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync(group);
            _groupParticipantRepoMock.Setup(x => x.GetByGroupAndUserAsync(_groupId, memberId))
                .ReturnsAsync(CreateParticipant(memberId, GroupRole.Member, true));
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.ApproveMemberAsync(memberId, _groupId, Guid.NewGuid()));
            Assert.Equal(ErrorCodes.GroupUpdatePermissionDenied, ex.Code);
        }

        [Fact]
        public async Task ApproveMemberAsync_TargetNotPending_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var group = CreateGroup();
            var targetId = Guid.NewGuid();
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync(group);
            _groupParticipantRepoMock.Setup(x => x.GetByGroupAndUserAsync(_groupId, _userId))
                .ReturnsAsync(CreateParticipant(_userId, GroupRole.Owner, true));
            _groupParticipantRepoMock.Setup(x => x.GetPendingByGroupAndUserAsync(_groupId, targetId))
                .ReturnsAsync((GroupParticipant?)null);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.ApproveMemberAsync(_userId, _groupId, targetId));
            Assert.Equal(ErrorCodes.GroupMemberNotFound, ex.Code);
        }

        [Fact]
        public async Task ApproveMemberAsync_CannotApproveOwner_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var group = CreateGroup();
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync(group);
            _groupParticipantRepoMock.Setup(x => x.GetByGroupAndUserAsync(_groupId, _userId))
                .ReturnsAsync(CreateParticipant(_userId, GroupRole.Owner, true));

            var ownerParticipant = CreateParticipant(_userId, GroupRole.Owner, false); // owner as pending
            _groupParticipantRepoMock.Setup(x => x.GetPendingByGroupAndUserAsync(_groupId, _userId))
                .ReturnsAsync(ownerParticipant);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.ApproveMemberAsync(_userId, _groupId, _userId));
            Assert.Equal(ErrorCodes.GroupMemberNotFound, ex.Code);
        }

        [Fact]
        public async Task ApproveMemberAsync_CannotSelfApprove_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var group = CreateGroup();
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync(group);
            _groupParticipantRepoMock.Setup(x => x.GetByGroupAndUserAsync(_groupId, _userId))
                .ReturnsAsync(CreateParticipant(_userId, GroupRole.Owner, true));

            var pendingSelf = CreateParticipant(_userId, GroupRole.Member, false);
            _groupParticipantRepoMock.Setup(x => x.GetPendingByGroupAndUserAsync(_groupId, _userId))
                .ReturnsAsync(pendingSelf);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.ApproveMemberAsync(_userId, _groupId, _userId));
            Assert.Equal(ErrorCodes.GroupMemberNotFound, ex.Code);
        }

        [Fact]
        public async Task ApproveMemberAsync_MemberLimitReached_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var group = CreateGroup();
            var targetId = Guid.NewGuid();
            var pendingParticipant = CreateParticipant(targetId, GroupRole.Member, false);

            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync(group);
            _groupParticipantRepoMock.Setup(x => x.GetByGroupAndUserAsync(_groupId, _userId))
                .ReturnsAsync(CreateParticipant(_userId, GroupRole.Owner, true));
            _groupParticipantRepoMock.Setup(x => x.GetPendingByGroupAndUserAsync(_groupId, targetId))
                .ReturnsAsync(pendingParticipant);
            _groupParticipantRepoMock.Setup(x => x.GetParticipantCountByGroupIdAsync(_groupId)).ReturnsAsync(10);
            _userSubscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync(CreateSubscriptionPlan(maxMembers: 10));
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.ApproveMemberAsync(_userId, _groupId, targetId));
            Assert.Equal(ErrorCodes.GroupMemberLimitReached, ex.Code);
        }

        [Fact]
        public async Task ApproveMemberAsync_ValidApproval_UpdatesAndInvalidatesCache()
        {
            // Arrange
            SetupDefaultMocks();
            var group = CreateGroup();
            var targetId = Guid.NewGuid();
            var pendingParticipant = CreateParticipant(targetId, GroupRole.Member, false);

            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync(group);
            _groupParticipantRepoMock.Setup(x => x.GetByGroupAndUserAsync(_groupId, _userId))
                .ReturnsAsync(CreateParticipant(_userId, GroupRole.Owner, true));
            _groupParticipantRepoMock.Setup(x => x.GetPendingByGroupAndUserAsync(_groupId, targetId))
                .ReturnsAsync(pendingParticipant);
            _groupParticipantRepoMock.Setup(x => x.GetParticipantCountByGroupIdAsync(_groupId)).ReturnsAsync(3);
            _userSubscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync(CreateSubscriptionPlan(maxMembers: 10));
            _groupParticipantRepoMock.Setup(x => x.UpdateAsync(It.IsAny<GroupParticipant>()))
                .Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(x => x.InvalidateAIMemberCacheAsync(_groupId))
                .Returns(Task.CompletedTask);
            _userRepoMock.Setup(x => x.GetByIdAsync(targetId))
                .ReturnsAsync(CreateUser("Target", "User", "target@test.com", "vi"));
            _emailServiceMock.Setup(x => x.SendLinkAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            var service = CreateService();

            // Act
            var result = await service.ApproveMemberAsync(_userId, _groupId, targetId);

            // Assert
            Assert.True(result.IsApproved);
            Assert.Equal(targetId, result.UserId);
            _groupParticipantRepoMock.Verify(x => x.UpdateAsync(It.Is<GroupParticipant>(
                p => p.UserId == targetId && p.IsApproved)), Times.Once);
        }

        [Fact]
        public async Task ApproveMemberAsync_GroupInStudio_AutoAddsToStudio()
        {
            // Arrange
            SetupDefaultMocks();
            var studio = CreateStudio();
            var group = CreateGroup();
            group.StudioId = _studioId;
            var targetId = Guid.NewGuid();
            var pendingParticipant = CreateParticipant(targetId, GroupRole.Member, false);

            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync(group);
            _groupParticipantRepoMock.Setup(x => x.GetByGroupAndUserAsync(_groupId, _userId))
                .ReturnsAsync(CreateParticipant(_userId, GroupRole.Owner, true));
            _groupParticipantRepoMock.Setup(x => x.GetPendingByGroupAndUserAsync(_groupId, targetId))
                .ReturnsAsync(pendingParticipant);
            _groupParticipantRepoMock.Setup(x => x.GetParticipantCountByGroupIdAsync(_groupId)).ReturnsAsync(3);
            _userSubscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync(CreateSubscriptionPlan(maxMembers: 10));
            _groupParticipantRepoMock.Setup(x => x.UpdateAsync(It.IsAny<GroupParticipant>()))
                .Returns(Task.CompletedTask);
            _cacheServiceMock.Setup(x => x.InvalidateAIMemberCacheAsync(_groupId))
                .Returns(Task.CompletedTask);
            _userRepoMock.Setup(x => x.GetByIdAsync(targetId))
                .ReturnsAsync(CreateUser("Target", "User", "target@test.com"));
            _emailServiceMock.Setup(x => x.SendLinkAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            // User not in studio → auto-add
            _studioParticipantRepoMock.Setup(x => x.GetPendingByStudioAndUserAsync(_studioId, targetId))
                .ReturnsAsync((StudioParticipant?)null);
            _studioParticipantRepoMock.Setup(x => x.IsUserApprovedInStudioAsync(_studioId, targetId))
                .ReturnsAsync(false);
            _studioParticipantRepoMock.Setup(x => x.AddAsync(It.IsAny<StudioParticipant>()))
                .Returns(Task.CompletedTask);
            var service = CreateService();

            // Act
            var result = await service.ApproveMemberAsync(_userId, _groupId, targetId);

            // Assert
            Assert.True(result.IsApproved);
            _studioParticipantRepoMock.Verify(x => x.AddAsync(It.Is<StudioParticipant>(
                sp => sp.UserId == targetId && sp.StudioId == _studioId)), Times.Once);
        }

        #endregion

        #region ToggleArchiveGroupAsync

        [Fact]
        public async Task ToggleArchiveGroupAsync_GroupNotFound_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync((Group?)null);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.ToggleArchiveGroupAsync(_userId, _groupId, true));
            Assert.Equal(ErrorCodes.GroupNotFound, ex.Code);
        }

        [Fact]
        public async Task ToggleArchiveGroupAsync_ModeratorCannotArchive_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var group = CreateGroup();
            var moderatorId = Guid.NewGuid();
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync(group);
            _groupRepoMock.Setup(x => x.IsUserGroupOwnerAsync(_groupId, moderatorId)).ReturnsAsync(false);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.ToggleArchiveGroupAsync(moderatorId, _groupId, true));
            Assert.Equal(ErrorCodes.GroupPermissionDenied, ex.Code);
        }

        [Fact]
        public async Task ToggleArchiveGroupAsync_OwnerArchives_Success()
        {
            // Arrange
            SetupDefaultMocks();
            var group = CreateGroup(isArchived: false);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync(group);
            _groupRepoMock.Setup(x => x.IsUserGroupOwnerAsync(_groupId, _userId)).ReturnsAsync(true);
            _groupRepoMock.Setup(x => x.UpdateAsync(It.IsAny<Group>())).Returns(Task.CompletedTask);
            var service = CreateService();

            // Act
            var result = await service.ToggleArchiveGroupAsync(_userId, _groupId, true);

            // Assert
            Assert.True(result.IsArchived);
            Assert.Equal(_groupId, result.GroupId);
        }

        [Fact]
        public async Task ToggleArchiveGroupAsync_OwnerUnarchives_Success()
        {
            // Arrange
            SetupDefaultMocks();
            var group = CreateGroup(isArchived: true);
            _groupRepoMock.Setup(x => x.GetByIdAsync(_groupId)).ReturnsAsync(group);
            _groupRepoMock.Setup(x => x.IsUserGroupOwnerAsync(_groupId, _userId)).ReturnsAsync(true);
            _groupRepoMock.Setup(x => x.UpdateAsync(It.IsAny<Group>())).Returns(Task.CompletedTask);
            var service = CreateService();

            // Act
            var result = await service.ToggleArchiveGroupAsync(_userId, _groupId, false);

            // Assert
            Assert.False(result.IsArchived);
        }

        #endregion

        #region CreateStudioGroupAsync

        [Fact]
        public async Task CreateStudioGroupAsync_StudioNotFound_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var studioId = Guid.NewGuid();
            var request = new CreateStudioGroupsRequest
            {
                StudioId = studioId,
                GroupPrefix = "Group",
                GroupCount = 3
            };
            _studioRepoMock.Setup(x => x.GetByIdAsync(studioId)).ReturnsAsync((Studio?)null);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.CreateStudioGroupAsync(_userId, request));
            Assert.Equal(ErrorCodes.StudioNotFound, ex.Code);
        }

        [Fact]
        public async Task CreateStudioGroupAsync_UserNotStudioOwner_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var studio = CreateStudio();
            studio.OwnerId = Guid.NewGuid(); // different owner
            var request = new CreateStudioGroupsRequest
            {
                StudioId = _studioId,
                GroupPrefix = "Group",
                GroupCount = 3
            };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioRepoMock.Setup(x => x.IsUserStudioOwnerAsync(_studioId, _userId)).ReturnsAsync(false);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.CreateStudioGroupAsync(_userId, request));
            Assert.Equal(ErrorCodes.StudioPermissionDenied, ex.Code);
        }

        [Fact]
        public async Task CreateStudioGroupAsync_TotalExceedsLimit_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var studio = CreateStudio();
            var request = new CreateStudioGroupsRequest
            {
                StudioId = _studioId,
                GroupPrefix = "Group",
                GroupCount = 10
            };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioRepoMock.Setup(x => x.IsUserStudioOwnerAsync(_studioId, _userId)).ReturnsAsync(true);
            _groupRepoMock.Setup(x => x.GetGroupCountByStudioIdAsync(_studioId)).ReturnsAsync(0);
            _userSubscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync(CreateSubscriptionPlan(maxGroups: 5));
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.CreateStudioGroupAsync(_userId, request));
            Assert.Equal(ErrorCodes.GroupLimitReached, ex.Code);
        }

        [Fact]
        public async Task CreateStudioGroupAsync_DuplicateGroupName_ThrowsAppException()
        {
            // Arrange
            SetupDefaultMocks();
            var studio = CreateStudio();
            var request = new CreateStudioGroupsRequest
            {
                StudioId = _studioId,
                GroupPrefix = "Existing",
                GroupCount = 3
            };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioRepoMock.Setup(x => x.IsUserStudioOwnerAsync(_studioId, _userId)).ReturnsAsync(true);
            _groupRepoMock.Setup(x => x.GetGroupCountByStudioIdAsync(_studioId)).ReturnsAsync(0);
            _userSubscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync(CreateSubscriptionPlan(maxGroups: 10));
            _groupRepoMock.Setup(x => x.GetGroupNamesInStudioAsync(_studioId))
                .ReturnsAsync(new List<string> { "Existing1", "Existing2" });
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                service.CreateStudioGroupAsync(_userId, request));
            Assert.Equal(ErrorCodes.GroupNameAlreadyExists, ex.Code);
        }

        [Fact]
        public async Task CreateStudioGroupAsync_ValidRequest_CreatesMultipleGroups()
        {
            // Arrange
            SetupDefaultMocks();
            var studio = CreateStudio();
            var request = new CreateStudioGroupsRequest
            {
                StudioId = _studioId,
                GroupPrefix = "Study",
                GroupCount = 3,
                Description = "Batch created groups"
            };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioRepoMock.Setup(x => x.IsUserStudioOwnerAsync(_studioId, _userId)).ReturnsAsync(true);
            _groupRepoMock.Setup(x => x.GetGroupCountByStudioIdAsync(_studioId)).ReturnsAsync(0);
            _userSubscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync(CreateSubscriptionPlan(maxGroups: 10));
            _groupRepoMock.Setup(x => x.GetGroupNamesInStudioAsync(_studioId))
                .ReturnsAsync(new List<string>());

            List<Group>? capturedGroups = new();
            _groupRepoMock.Setup(x => x.AddAsync(It.IsAny<Group>()))
                .Callback<Group>(g => capturedGroups.Add(g))
                .Returns(Task.CompletedTask);
            _groupParticipantRepoMock.Setup(x => x.AddAsync(It.IsAny<GroupParticipant>()))
                .Returns(Task.CompletedTask);

            var service = CreateService();

            // Act
            var result = await service.CreateStudioGroupAsync(_userId, request);

            // Assert
            Assert.Equal(3, result.CreateGroups.Count);
            Assert.Equal(3, capturedGroups.Count);
            Assert.Equal("Study1", result.CreateGroups[0].GroupName);
            Assert.Equal("Study2", result.CreateGroups[1].GroupName);
            Assert.Equal("Study3", result.CreateGroups[2].GroupName);
        }

        [Fact]
        public async Task CreateStudioGroupAsync_UsesBrandColors_CyclesThroughArray()
        {
            // Arrange
            SetupDefaultMocks();
            var studio = CreateStudio();
            var request = new CreateStudioGroupsRequest
            {
                StudioId = _studioId,
                GroupPrefix = "Study",
                GroupCount = 3
            };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioRepoMock.Setup(x => x.IsUserStudioOwnerAsync(_studioId, _userId)).ReturnsAsync(true);
            _groupRepoMock.Setup(x => x.GetGroupCountByStudioIdAsync(_studioId)).ReturnsAsync(0);
            _userSubscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync(CreateSubscriptionPlan(maxGroups: 10));
            _groupRepoMock.Setup(x => x.GetGroupNamesInStudioAsync(_studioId))
                .ReturnsAsync(new List<string>());

            List<Group>? capturedGroups = new();
            _groupRepoMock.Setup(x => x.AddAsync(It.IsAny<Group>()))
                .Callback<Group>(g => capturedGroups.Add(g))
                .Returns(Task.CompletedTask);
            _groupParticipantRepoMock.Setup(x => x.AddAsync(It.IsAny<GroupParticipant>()))
                .Returns(Task.CompletedTask);

            var service = CreateService();

            // Act
            await service.CreateStudioGroupAsync(_userId, request);

            // Assert
            // First group: colorIndex = (1 - 0 - 1) % 12 = 0 → "#FF5F3D"
            Assert.Equal("#FF5F3D", capturedGroups[0].ColorHex);
            Assert.Equal("#FF7A54", capturedGroups[1].ColorHex);
            Assert.Equal("#FF4D6A", capturedGroups[2].ColorHex);
        }

        #endregion
    }
}
