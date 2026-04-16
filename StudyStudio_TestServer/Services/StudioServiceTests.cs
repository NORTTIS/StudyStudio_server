using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services;
using StudioStudio_Server.Services.Interfaces;
using Xunit;

namespace StudioStudio_Server.Tests.Services
{
    public class StudioServiceTests
    {
        private readonly Mock<IStudioRepository> _studioRepoMock;
        private readonly Mock<IGroupRepository> _groupRepoMock;
        private readonly Mock<IUserSubscriptionRepository> _subscriptionRepoMock;
        private readonly Mock<IStudioParticipantRepository> _studioParticipantRepoMock;
        private readonly Mock<IGroupParticipantRepository> _groupParticipantRepoMock;
        private readonly Mock<IUserRepository> _userRepoMock;
        private readonly Mock<IEmailService> _emailServiceMock;
        private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
        private readonly Mock<ILogger<StudioService>> _loggerMock;
        private readonly Mock<IConfiguration> _configMock;
        private readonly Mock<ICacheService> _cacheServiceMock;
        private StudioService _service = null!;

        // Fixed test IDs
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _ownerId = Guid.NewGuid();
        private readonly Guid _studioId = Guid.NewGuid();
        private readonly Guid _groupId = Guid.NewGuid();
        private readonly Guid _targetUserId = Guid.NewGuid();

        public StudioServiceTests()
        {
            _studioRepoMock = new Mock<IStudioRepository>();
            _groupRepoMock = new Mock<IGroupRepository>();
            _subscriptionRepoMock = new Mock<IUserSubscriptionRepository>();
            _studioParticipantRepoMock = new Mock<IStudioParticipantRepository>();
            _groupParticipantRepoMock = new Mock<IGroupParticipantRepository>();
            _userRepoMock = new Mock<IUserRepository>();
            _emailServiceMock = new Mock<IEmailService>();
            _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
            _loggerMock = new Mock<ILogger<StudioService>>();
            _configMock = new Mock<IConfiguration>();
            _cacheServiceMock = new Mock<ICacheService>();

            _configMock.Setup(x => x["Frontend:BaseUrl"]).Returns("http://localhost:3000");

            _service = new StudioService(
                _studioRepoMock.Object,
                _groupRepoMock.Object,
                _subscriptionRepoMock.Object,
                _studioParticipantRepoMock.Object,
                _groupParticipantRepoMock.Object,
                _userRepoMock.Object,
                _emailServiceMock.Object,
                _httpContextAccessorMock.Object,
                _loggerMock.Object,
                _configMock.Object,
                _cacheServiceMock.Object);
        }

        #region GetUserStudiosAsync

        [Fact]
        public async Task GetUserStudiosAsync_NoStudios_ReturnsEmptyList()
        {
            // Arrange
            _subscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync((SubscriptionPlan?)null);
            _studioRepoMock.Setup(x => x.CountStudioCreatedByUserAsync(_userId)).ReturnsAsync(0);
            _studioRepoMock.Setup(x => x.GetByOwnerIdAsync(_userId))
                .ReturnsAsync(new List<Studio>());
            _studioParticipantRepoMock.Setup(x => x.GetStudiosByUserIdAsync(_userId))
                .ReturnsAsync(new List<StudioParticipant>());

            // Act
            var result = await _service.GetUserStudiosAsync(_userId);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.Studios);
            Assert.Equal(3, result.Subscription.StudioLimit); // Default Free plan
            Assert.Equal(0, result.Subscription.StudioCreated);
        }

        [Fact]
        public async Task GetUserStudiosAsync_WithOwnedStudios_ReturnsStudios()
        {
            // Arrange
            var studio = new Studio
            {
                StudioId = _studioId,
                OwnerId = _userId,
                StudioName = "My Studio",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsOpen = true,
                IsArchived = false
            };
            _subscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync((SubscriptionPlan?)null);
            _studioRepoMock.Setup(x => x.CountStudioCreatedByUserAsync(_userId)).ReturnsAsync(1);
            _studioRepoMock.Setup(x => x.GetByOwnerIdAsync(_userId))
                .ReturnsAsync(new List<Studio> { studio });
            _studioParticipantRepoMock.Setup(x => x.GetStudiosByUserIdAsync(_userId))
                .ReturnsAsync(new List<StudioParticipant>());
            _groupRepoMock.Setup(x => x.GetGroupCountByStudioIdAsync(_studioId)).ReturnsAsync(5);
            _studioParticipantRepoMock.Setup(x => x.GetParticipantCountByStudioIdAsync(_studioId)).ReturnsAsync(3);

            // Act
            var result = await _service.GetUserStudiosAsync(_userId);

            // Assert
            Assert.Single(result.Studios);
            Assert.Equal("My Studio", result.Studios[0].StudioName);
            Assert.Equal(StudioRole.Owner, result.Studios[0].StudioRole);
            Assert.Equal(5, result.Studios[0].GroupCount);
            Assert.Equal(3, result.Studios[0].MemberCount);
        }

        [Fact]
        public async Task GetUserStudiosAsync_WithMemberStudios_ReturnsStudios()
        {
            // Arrange
            var studio = new Studio
            {
                StudioId = _studioId,
                OwnerId = _ownerId,
                StudioName = "Member Studio",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsOpen = true,
                IsArchived = false
            };
            var participant = new StudioParticipant
            {
                StudioId = _studioId,
                UserId = _userId,
                IsApproved = true,
                Role = StudioRole.Member
            };
            _subscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync((SubscriptionPlan?)null);
            _studioRepoMock.Setup(x => x.CountStudioCreatedByUserAsync(_userId)).ReturnsAsync(0);
            _studioRepoMock.Setup(x => x.GetByOwnerIdAsync(_userId))
                .ReturnsAsync(new List<Studio>());
            _studioParticipantRepoMock.Setup(x => x.GetStudiosByUserIdAsync(_userId))
                .ReturnsAsync(new List<StudioParticipant> { participant });
            _studioRepoMock.Setup(x => x.GetByIdsAsync(It.IsAny<List<Guid>>()))
                .ReturnsAsync(new List<Studio> { studio });
            _groupRepoMock.Setup(x => x.GetGroupCountByStudioIdAsync(_studioId)).ReturnsAsync(2);
            _studioParticipantRepoMock.Setup(x => x.GetParticipantCountByStudioIdAsync(_studioId)).ReturnsAsync(4);

            // Act
            var result = await _service.GetUserStudiosAsync(_userId);

            // Assert
            Assert.Single(result.Studios);
            Assert.Equal("Member Studio", result.Studios[0].StudioName);
            Assert.Equal(StudioRole.Member, result.Studios[0].StudioRole);
            Assert.True(result.Studios[0].IsMember);
        }

        #endregion

        #region CreateStudioAsync

        [Fact]
        public async Task CreateStudioAsync_StudioLimitReached_ThrowsForbidden()
        {
            // Arrange
            var plan = new SubscriptionPlan { MaxStudios = 1 };
            _subscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_ownerId))
                .ReturnsAsync(plan);
            _studioRepoMock.Setup(x => x.CountStudioCreatedByUserAsync(_ownerId)).ReturnsAsync(1);

            var request = new CreateStudioRequest { StudioName = "New Studio" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.CreateStudioAsync(_ownerId, request));
            Assert.Equal(ErrorCodes.StudioLimitReached, ex.Code);
            Assert.Equal(403, ex.HttpStatus);
        }

        [Fact]
        public async Task CreateStudioAsync_InvalidDateRange_ThrowsBadRequest()
        {
            // Arrange
            _subscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_ownerId))
                .ReturnsAsync((SubscriptionPlan?)null);
            _studioRepoMock.Setup(x => x.CountStudioCreatedByUserAsync(_ownerId)).ReturnsAsync(0);

            var request = new CreateStudioRequest
            {
                StudioName = "Test Studio",
                StartDate = new DateTime(2026, 4, 20),
                EndDate = new DateTime(2026, 4, 10) // Before start
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.CreateStudioAsync(_ownerId, request));
            Assert.Equal(ErrorCodes.StudioInvalidDateRange, ex.Code);
        }

        [Fact]
        public async Task CreateStudioAsync_NameTooLong_ThrowsBadRequest()
        {
            // Arrange
            _subscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_ownerId))
                .ReturnsAsync((SubscriptionPlan?)null);
            _studioRepoMock.Setup(x => x.CountStudioCreatedByUserAsync(_ownerId)).ReturnsAsync(0);

            var request = new CreateStudioRequest
            {
                StudioName = new string('A', 256) // > 255 chars
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.CreateStudioAsync(_ownerId, request));
            Assert.Equal(ErrorCodes.StudioNameInvalid, ex.Code);
        }

        [Fact]
        public async Task CreateStudioAsync_DescriptionTooLong_ThrowsBadRequest()
        {
            // Arrange
            _subscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_ownerId))
                .ReturnsAsync((SubscriptionPlan?)null);
            _studioRepoMock.Setup(x => x.CountStudioCreatedByUserAsync(_ownerId)).ReturnsAsync(0);

            var request = new CreateStudioRequest
            {
                StudioName = "Test Studio",
                Description = new string('A', 501) // > 500 chars
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.CreateStudioAsync(_ownerId, request));
            Assert.Equal(ErrorCodes.StudioDescriptionInvalid, ex.Code);
        }

        [Fact]
        public async Task CreateStudioAsync_NameAlreadyExists_ThrowsBadRequest()
        {
            // Arrange
            _subscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_ownerId))
                .ReturnsAsync((SubscriptionPlan?)null);
            _studioRepoMock.Setup(x => x.CountStudioCreatedByUserAsync(_ownerId)).ReturnsAsync(0);
            _studioRepoMock.Setup(x => x.IsStudioNameExistByOwnerIdAsync("Existing Studio", _ownerId))
                .ReturnsAsync(true);

            var request = new CreateStudioRequest { StudioName = "Existing Studio" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.CreateStudioAsync(_ownerId, request));
            Assert.Equal(ErrorCodes.StudioNameAlreadyExist, ex.Code);
        }

        [Fact]
        public async Task CreateStudioAsync_ValidRequest_CreatesStudioAndParticipant()
        {
            // Arrange
            _subscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_ownerId))
                .ReturnsAsync((SubscriptionPlan?)null);
            _studioRepoMock.Setup(x => x.CountStudioCreatedByUserAsync(_ownerId)).ReturnsAsync(0);
            _studioRepoMock.Setup(x => x.IsStudioNameExistByOwnerIdAsync("New Studio", _ownerId))
                .ReturnsAsync(false);

            var request = new CreateStudioRequest
            {
                StudioName = "New Studio",
                Description = "A new studio",
                IsOpen = true
            };

            // Act
            var result = await _service.CreateStudioAsync(_ownerId, request);

            // Assert
            Assert.Equal("New Studio", result.StudioName);
            Assert.Equal(_ownerId, result.OwnerId);
            Assert.Equal(0, result.GroupCount);
            Assert.Equal(1, result.MemberCount);
            Assert.False(result.IsArchived);
            _studioRepoMock.Verify(x => x.CreateStudioAsync(It.IsAny<Studio>()), Times.Once);
            _studioParticipantRepoMock.Verify(x => x.AddAsync(It.IsAny<StudioParticipant>()), Times.Once);
        }

        #endregion

        #region GetStudioDetailAsync

        [Fact]
        public async Task GetStudioDetailAsync_StudioNotFound_ThrowsNotFound()
        {
            // Arrange
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId))
                .ReturnsAsync((Studio?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.GetStudioDetailAsync(_userId, _studioId));
            Assert.Equal(ErrorCodes.StudioNotFound, ex.Code);
        }

        [Fact]
        public async Task GetStudioDetailAsync_UserNotOwnerNorMember_ThrowsForbidden()
        {
            // Arrange
            var studio = new Studio { StudioId = _studioId, OwnerId = _ownerId };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioParticipantRepoMock.Setup(x => x.GetByStudioAndUserAsync(_studioId, _userId))
                .ReturnsAsync((StudioParticipant?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.GetStudioDetailAsync(_userId, _studioId));
            Assert.Equal(ErrorCodes.AuthForbidden, ex.Code);
        }

        [Fact]
        public async Task GetStudioDetailAsync_Owner_ReturnsStudioDetail()
        {
            // Arrange
            var studio = new Studio
            {
                StudioId = _studioId,
                OwnerId = _userId,
                StudioName = "Owner Studio",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsOpen = true,
                IsArchived = false
            };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _groupRepoMock.Setup(x => x.GetGroupCountByStudioIdAsync(_studioId)).ReturnsAsync(7);
            _studioParticipantRepoMock.Setup(x => x.GetParticipantCountByStudioIdAsync(_studioId))
                .ReturnsAsync(5);

            // Act
            var result = await _service.GetStudioDetailAsync(_userId, _studioId);

            // Assert
            Assert.Equal("Owner Studio", result.StudioName);
            Assert.Equal(StudioRole.Owner, result.StudioRole);
            Assert.Equal(7, result.GroupCount);
            Assert.Equal(5, result.MemberCount);
        }

        [Fact]
        public async Task GetStudioDetailAsync_Member_SeesOnlyTheirGroups()
        {
            // Arrange
            var studio = new Studio
            {
                StudioId = _studioId,
                OwnerId = _ownerId,
                StudioName = "Member Studio",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsOpen = true,
                IsArchived = false
            };
            var participant = new StudioParticipant { StudioId = _studioId, UserId = _userId, IsApproved = true };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioParticipantRepoMock.Setup(x => x.GetByStudioAndUserAsync(_studioId, _userId))
                .ReturnsAsync(participant);

            var group1 = new Group { GroupId = Guid.NewGuid(), StudioId = _studioId };
            var group2 = new Group { GroupId = Guid.NewGuid(), StudioId = _studioId };
            _groupRepoMock.Setup(x => x.GetStudioGroupsAsync(_studioId))
                .ReturnsAsync(new List<Group> { group1, group2 });

            var userGroupParticipant = new GroupParticipant { GroupId = group1.GroupId, UserId = _userId };
            _groupParticipantRepoMock.Setup(x => x.GetByGroupIdsAsync(It.IsAny<List<Guid>>()))
                .ReturnsAsync(new List<GroupParticipant> { userGroupParticipant });

            _studioParticipantRepoMock.Setup(x => x.GetParticipantCountByStudioIdAsync(_studioId))
                .ReturnsAsync(3);

            // Act
            var result = await _service.GetStudioDetailAsync(_userId, _studioId);

            // Assert
            Assert.Equal(StudioRole.Member, result.StudioRole);
            Assert.Equal(1, result.GroupCount); // Only groups they participate in
        }

        #endregion

        #region DeleteStudioAsync

        [Fact]
        public async Task DeleteStudioAsync_StudioNotFound_ThrowsNotFound()
        {
            // Arrange
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId))
                .ReturnsAsync((Studio?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.DeleteStudioAsync(_userId, _studioId));
            Assert.Equal(ErrorCodes.StudioNotFound, ex.Code);
        }

        [Fact]
        public async Task DeleteStudioAsync_NotOwner_ThrowsForbidden()
        {
            // Arrange
            var studio = new Studio { StudioId = _studioId, OwnerId = _ownerId };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.DeleteStudioAsync(_userId, _studioId));
            Assert.Equal(ErrorCodes.AuthForbidden, ex.Code);
        }

        [Fact]
        public async Task DeleteStudioAsync_Owner_DeletesStudioAndGroups()
        {
            // Arrange
            var studio = new Studio { StudioId = _studioId, OwnerId = _userId };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _groupRepoMock.Setup(x => x.GetStudioGroupsAsync(_studioId))
                .ReturnsAsync(new List<Group> { new Group { GroupId = _groupId, StudioId = _studioId } });

            // Act
            await _service.DeleteStudioAsync(_userId, _studioId);

            // Assert
            _groupRepoMock.Verify(x => x.SaveChangesAsync(), Times.Once);
            _studioRepoMock.Verify(x => x.DeleteStudioAsync(studio), Times.Once);
        }

        #endregion

        #region UpdateStudioAsync

        [Fact]
        public async Task UpdateStudioAsync_StudioNotFound_ThrowsNotFound()
        {
            // Arrange
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId))
                .ReturnsAsync((Studio?)null);

            var request = new UpdateStudioRequest { Id = _studioId, StudioName = "Updated" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateStudioAsync(_userId, request));
            Assert.Equal(ErrorCodes.StudioNotFound, ex.Code);
        }

        [Fact]
        public async Task UpdateStudioAsync_NotOwner_ThrowsForbidden()
        {
            // Arrange
            var studio = new Studio { StudioId = _studioId, OwnerId = _ownerId };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);

            var request = new UpdateStudioRequest { Id = _studioId, StudioName = "Updated" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateStudioAsync(_userId, request));
            Assert.Equal(ErrorCodes.AuthForbidden, ex.Code);
        }

        [Fact]
        public async Task UpdateStudioAsync_InvalidColor_ThrowsBadRequest()
        {
            // Arrange
            var studio = new Studio
            {
                StudioId = _studioId,
                OwnerId = _userId,
                StudioName = "Old Name",
                ColorHex = "#FFFFFF"
            };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioRepoMock.Setup(x => x.IsStudioNameExistExcludingStudioAsync("Updated", _userId, _studioId))
                .ReturnsAsync(false);

            var request = new UpdateStudioRequest
            {
                Id = _studioId,
                StudioName = "Updated",
                ColorHex = "invalid-color"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateStudioAsync(_userId, request));
            Assert.Equal(ErrorCodes.ValidationInvalidColor, ex.Code);
        }

        [Fact]
        public async Task UpdateStudioAsync_InvalidBannerUrl_ThrowsBadRequest()
        {
            // Arrange
            var studio = new Studio
            {
                StudioId = _studioId,
                OwnerId = _userId,
                StudioName = "Old Name"
            };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioRepoMock.Setup(x => x.IsStudioNameExistExcludingStudioAsync("Updated", _userId, _studioId))
                .ReturnsAsync(false);

            var request = new UpdateStudioRequest
            {
                Id = _studioId,
                StudioName = "Updated",
                BannerUrl = "not-a-valid-url"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateStudioAsync(_userId, request));
            Assert.Equal(ErrorCodes.ValidationInvalidBannerUrl, ex.Code);
        }

        [Fact]
        public async Task UpdateStudioAsync_InvalidAlias_ThrowsBadRequest()
        {
            // Arrange
            var studio = new Studio
            {
                StudioId = _studioId,
                OwnerId = _userId,
                StudioName = "Old Name"
            };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioRepoMock.Setup(x => x.IsStudioNameExistExcludingStudioAsync("Updated", _userId, _studioId))
                .ReturnsAsync(false);

            var request = new UpdateStudioRequest
            {
                Id = _studioId,
                StudioName = "Updated",
                Alias = "invalid alias with spaces!" // Only alphanumeric, spaces, Vietnamese, _, -
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateStudioAsync(_userId, request));
            Assert.Equal(ErrorCodes.ValidationInvalidAlias, ex.Code);
        }

        [Fact]
        public async Task UpdateStudioAsync_AliasTooLong_ThrowsBadRequest()
        {
            // Arrange
            var studio = new Studio
            {
                StudioId = _studioId,
                OwnerId = _userId,
                StudioName = "Old Name"
            };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioRepoMock.Setup(x => x.IsStudioNameExistExcludingStudioAsync("Updated", _userId, _studioId))
                .ReturnsAsync(false);

            var request = new UpdateStudioRequest
            {
                Id = _studioId,
                StudioName = "Updated",
                Alias = new string('A', 51) // > 50 chars
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateStudioAsync(_userId, request));
            Assert.Equal(ErrorCodes.ValidationStringLength, ex.Code);
        }

        [Fact]
        public async Task UpdateStudioAsync_ValidRequest_UpdatesAndInvalidatesCache()
        {
            // Arrange
            var studio = new Studio
            {
                StudioId = _studioId,
                OwnerId = _userId,
                StudioName = "Old Name",
                Description = "Old description",
                ColorHex = "#FFFFFF",
                IsOpen = false,
                IsArchived = false
            };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioRepoMock.Setup(x => x.IsStudioNameExistExcludingStudioAsync("New Studio", _userId, _studioId))
                .ReturnsAsync(false);

            var request = new UpdateStudioRequest
            {
                Id = _studioId,
                StudioName = "New Studio",
                Description = "New description",
                ColorHex = "#FF0000",
                IsOpen = true
            };

            // Act
            var result = await _service.UpdateStudioAsync(_userId, request);

            // Assert
            Assert.Equal("New Studio", result.StudioName);
            Assert.Equal("New description", result.Description);
            Assert.Equal("#FF0000", result.ColorHex);
            Assert.True(result.IsOpen);
            _studioRepoMock.Verify(x => x.UpdateStudioAsync(It.IsAny<Studio>()), Times.Once);
            _cacheServiceMock.Verify(x => x.InvalidateAIStudioCacheAsync(_studioId), Times.Once);
        }

        #endregion

        #region GetStudioMembersAsync

        [Fact]
        public async Task GetStudioMembersAsync_UserNotMember_ThrowsForbidden()
        {
            // Arrange
            _studioParticipantRepoMock.Setup(x => x.GetByStudioAndUserAsync(_studioId, _userId))
                .ReturnsAsync((StudioParticipant?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.GetStudioMembersAsync(_userId, _studioId));
            Assert.Equal(ErrorCodes.AuthForbidden, ex.Code);
        }

        [Fact]
        public async Task GetStudioMembersAsync_ValidMember_ReturnsMembers()
        {
            // Arrange
            var participant = new StudioParticipant
            {
                StudioId = _studioId,
                UserId = _userId,
                Role = StudioRole.Member,
                IsApproved = true,
                User = new User { UserId = _userId, FirstName = "John", LastName = "Doe", Email = "john@test.com" }
            };
            _studioParticipantRepoMock.Setup(x => x.GetByStudioAndUserAsync(_studioId, _userId))
                .ReturnsAsync(participant);
            _studioParticipantRepoMock.Setup(x => x.GetParticipantsByStudioIdAsync(_studioId))
                .ReturnsAsync(new List<StudioParticipant> { participant });
            _groupRepoMock.Setup(x => x.GetStudioGroupsAsync(_studioId))
                .ReturnsAsync(new List<Group>());
            _groupParticipantRepoMock.Setup(x => x.GetByGroupIdsAsync(It.IsAny<List<Guid>>()))
                .ReturnsAsync(new List<GroupParticipant>());

            // Act
            var result = await _service.GetStudioMembersAsync(_userId, _studioId);

            // Assert
            Assert.Single(result);
            Assert.Equal(_userId, result[0].UserId);
            Assert.Equal(StudioRole.Member, result[0].StudioRole);
        }

        #endregion

        #region LeaveStudioAsync

        [Fact]
        public async Task LeaveStudioAsync_StudioNotFound_ThrowsNotFound()
        {
            // Arrange
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId))
                .ReturnsAsync((Studio?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.LeaveStudioAsync(_userId, _studioId));
            Assert.Equal(ErrorCodes.StudioNotFound, ex.Code);
        }

        [Fact]
        public async Task LeaveStudioAsync_UserNotMember_ThrowsNotFound()
        {
            // Arrange
            var studio = new Studio { StudioId = _studioId, OwnerId = _ownerId };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioParticipantRepoMock.Setup(x => x.GetByStudioAndUserIncludeNonApprovedAsync(_studioId, _userId))
                .ReturnsAsync((StudioParticipant?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.LeaveStudioAsync(_userId, _studioId));
            Assert.Equal(ErrorCodes.StudioNotFound, ex.Code);
        }

        [Fact]
        public async Task LeaveStudioAsync_OwnerCannotLeave_ThrowsForbidden()
        {
            // Arrange
            var studio = new Studio { StudioId = _studioId, OwnerId = _userId };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioParticipantRepoMock.Setup(x => x.GetByStudioAndUserIncludeNonApprovedAsync(_studioId, _userId))
                .ReturnsAsync(new StudioParticipant { StudioId = _studioId, UserId = _userId, Role = StudioRole.Owner });

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.LeaveStudioAsync(_userId, _studioId));
            Assert.Equal(ErrorCodes.StudioCannotLeaveAsOwner, ex.Code);
        }

        [Fact]
        public async Task LeaveStudioAsync_ValidMember_LeavesStudioAndGroups()
        {
            // Arrange
            var studio = new Studio { StudioId = _studioId, OwnerId = _ownerId, StudioName = "Test Studio" };
            var participant = new StudioParticipant
            {
                StudioId = _studioId,
                UserId = _userId,
                Role = StudioRole.Member,
                IsApproved = true
            };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioParticipantRepoMock.Setup(x => x.GetByStudioAndUserIncludeNonApprovedAsync(_studioId, _userId))
                .ReturnsAsync(participant);

            var group = new Group { GroupId = _groupId, StudioId = _studioId };
            _groupRepoMock.Setup(x => x.GetStudioGroupsAsync(_studioId))
                .ReturnsAsync(new List<Group> { group });
            _groupParticipantRepoMock.Setup(x => x.GetByGroupIdsAsync(It.IsAny<List<Guid>>()))
                .ReturnsAsync(new List<GroupParticipant>
                {
                    new() { GroupId = _groupId, UserId = _userId, Role = GroupRole.Member }
                });

            // Act
            var result = await _service.LeaveStudioAsync(_userId, _studioId);

            // Assert
            Assert.Equal(_studioId, result.StudioId);
            Assert.Equal("Test Studio", result.StudioName);
            _studioParticipantRepoMock.Verify(x => x.RemoveAsync(participant), Times.Once);
            _groupParticipantRepoMock.Verify(x => x.RemoveRangeAsync(It.IsAny<List<GroupParticipant>>()), Times.Once);
        }

        #endregion

        #region ToggleIsOpenAsync

        [Fact]
        public async Task ToggleIsOpenAsync_StudioNotFound_ThrowsNotFound()
        {
            // Arrange
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId))
                .ReturnsAsync((Studio?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ToggleIsOpenAsync(_userId, _studioId, true));
            Assert.Equal(ErrorCodes.StudioNotFound, ex.Code);
        }

        [Fact]
        public async Task ToggleIsOpenAsync_NotOwner_ThrowsForbidden()
        {
            // Arrange
            var studio = new Studio { StudioId = _studioId, OwnerId = _ownerId };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ToggleIsOpenAsync(_userId, _studioId, true));
            Assert.Equal(ErrorCodes.AuthForbidden, ex.Code);
        }

        [Fact]
        public async Task ToggleIsOpenAsync_Owner_UpdatesAndInvalidatesCache()
        {
            // Arrange
            var studio = new Studio
            {
                StudioId = _studioId,
                OwnerId = _userId,
                StudioName = "Test Studio",
                IsOpen = false
            };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);

            // Act
            var result = await _service.ToggleIsOpenAsync(_userId, _studioId, true);

            // Assert
            Assert.True(result.IsOpen);
            _studioRepoMock.Verify(x => x.UpdateStudioAsync(It.IsAny<Studio>()), Times.Once);
            _cacheServiceMock.Verify(x => x.InvalidateAIStudioCacheAsync(_studioId), Times.Once);
        }

        #endregion

        #region GetPendingMembersAsync

        [Fact]
        public async Task GetPendingMembersAsync_StudioNotFound_ThrowsNotFound()
        {
            // Arrange
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId))
                .ReturnsAsync((Studio?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.GetPendingMembersAsync(_userId, _studioId));
            Assert.Equal(ErrorCodes.StudioNotFound, ex.Code);
        }

        [Fact]
        public async Task GetPendingMembersAsync_NotOwner_ThrowsForbidden()
        {
            // Arrange
            var studio = new Studio { StudioId = _studioId, OwnerId = _ownerId };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.GetPendingMembersAsync(_userId, _studioId));
            Assert.Equal(ErrorCodes.AuthForbidden, ex.Code);
        }

        [Fact]
        public async Task GetPendingMembersAsync_Owner_ReturnsPendingMembers()
        {
            // Arrange
            var studio = new Studio { StudioId = _studioId, OwnerId = _userId, StudioName = "Test" };
            var pendingUser = new User { UserId = _targetUserId, FirstName = "Jane", LastName = "Doe", Email = "jane@test.com" };
            var pending = new StudioParticipant
            {
                StudioId = _studioId,
                UserId = _targetUserId,
                User = pendingUser,
                IsApproved = false,
                CreatedAt = DateTime.UtcNow
            };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioParticipantRepoMock.Setup(x => x.GetPendingByStudioIdAsync(_studioId))
                .ReturnsAsync(new List<StudioParticipant> { pending });

            // Act
            var result = await _service.GetPendingMembersAsync(_userId, _studioId);

            // Assert
            Assert.Equal(_studioId, result.StudioId);
            Assert.Equal(1, result.TotalPending);
            Assert.Single(result.PendingMembers);
        }

        #endregion

        #region RemoveMemberAsync

        [Fact]
        public async Task RemoveMemberAsync_StudioNotFound_ThrowsNotFound()
        {
            // Arrange
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId))
                .ReturnsAsync((Studio?)null);

            var request = new RemoveStudioMemberRequest { StudioId = _studioId, UserId = _targetUserId };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.RemoveMemberAsync(_userId, request));
            Assert.Equal(ErrorCodes.StudioNotFound, ex.Code);
        }

        [Fact]
        public async Task RemoveMemberAsync_NotOwner_ThrowsForbidden()
        {
            // Arrange
            var studio = new Studio { StudioId = _studioId, OwnerId = _ownerId };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);

            var request = new RemoveStudioMemberRequest { StudioId = _studioId, UserId = _targetUserId };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.RemoveMemberAsync(_userId, request));
            Assert.Equal(ErrorCodes.AuthForbidden, ex.Code);
        }

        [Fact]
        public async Task RemoveMemberAsync_RemoveSelf_ThrowsBadRequest()
        {
            // Arrange
            var studio = new Studio { StudioId = _studioId, OwnerId = _userId };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);

            var request = new RemoveStudioMemberRequest { StudioId = _studioId, UserId = _userId };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.RemoveMemberAsync(_userId, request));
            Assert.Equal(ErrorCodes.StudioCannotRemoveSelf, ex.Code);
        }

        [Fact]
        public async Task RemoveMemberAsync_MemberNotFound_ThrowsNotFound()
        {
            // Arrange
            var studio = new Studio { StudioId = _studioId, OwnerId = _userId };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioParticipantRepoMock.Setup(x => x.GetByStudioAndUserTrackedAsync(_studioId, _targetUserId))
                .ReturnsAsync((StudioParticipant?)null);

            var request = new RemoveStudioMemberRequest { StudioId = _studioId, UserId = _targetUserId };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.RemoveMemberAsync(_userId, request));
            Assert.Equal(ErrorCodes.StudioMemberNotFound, ex.Code);
        }

        [Fact]
        public async Task RemoveMemberAsync_RemoveOwner_ThrowsBadRequest()
        {
            // Arrange
            var studio = new Studio { StudioId = _studioId, OwnerId = _userId };
            var targetMember = new StudioParticipant
            {
                StudioId = _studioId,
                UserId = _targetUserId,
                Role = StudioRole.Owner
            };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioParticipantRepoMock.Setup(x => x.GetByStudioAndUserTrackedAsync(_studioId, _targetUserId))
                .ReturnsAsync(targetMember);

            var request = new RemoveStudioMemberRequest { StudioId = _studioId, UserId = _targetUserId };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.RemoveMemberAsync(_userId, request));
            Assert.Equal(ErrorCodes.StudioCannotRemoveOwner, ex.Code);
        }

        [Fact]
        public async Task RemoveMemberAsync_ValidMember_RemovesAndInvalidatesCache()
        {
            // Arrange
            var studio = new Studio { StudioId = _studioId, OwnerId = _userId, StudioName = "Test Studio" };
            var targetMember = new StudioParticipant
            {
                StudioId = _studioId,
                UserId = _targetUserId,
                Role = StudioRole.Member
            };
            var removedUser = new User { UserId = _targetUserId, FirstName = "Jane", LastName = "Doe" };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioParticipantRepoMock.Setup(x => x.GetByStudioAndUserTrackedAsync(_studioId, _targetUserId))
                .ReturnsAsync(targetMember);
            _groupRepoMock.Setup(x => x.GetStudioGroupsAsync(_studioId))
                .ReturnsAsync(new List<Group>());
            _userRepoMock.Setup(x => x.GetByIdAsync(_targetUserId)).ReturnsAsync(removedUser);

            var request = new RemoveStudioMemberRequest { StudioId = _studioId, UserId = _targetUserId };

            // Act
            var result = await _service.RemoveMemberAsync(_userId, request);

            // Assert
            Assert.Equal(_targetUserId, result.RemovedUserId);
            Assert.Equal("Jane Doe", result.RemovedUserName);
            _studioParticipantRepoMock.Verify(x => x.RemoveAsync(targetMember), Times.Once);
        }

        #endregion

        #region ApproveMemberAsync

        [Fact]
        public async Task ApproveMemberAsync_StudioNotFound_ThrowsNotFound()
        {
            // Arrange
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId))
                .ReturnsAsync((Studio?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ApproveMemberAsync(_userId, _studioId, _targetUserId));
            Assert.Equal(ErrorCodes.StudioNotFound, ex.Code);
        }

        [Fact]
        public async Task ApproveMemberAsync_NotOwner_ThrowsForbidden()
        {
            // Arrange
            var studio = new Studio { StudioId = _studioId, OwnerId = _ownerId };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ApproveMemberAsync(_userId, _studioId, _targetUserId));
            Assert.Equal(ErrorCodes.AuthForbidden, ex.Code);
        }

        [Fact]
        public async Task ApproveMemberAsync_PendingMemberNotFound_ThrowsNotFound()
        {
            // Arrange
            var studio = new Studio { StudioId = _studioId, OwnerId = _userId };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioParticipantRepoMock.Setup(x => x.GetPendingByStudioAndUserAsync(_studioId, _targetUserId))
                .ReturnsAsync((StudioParticipant?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ApproveMemberAsync(_userId, _studioId, _targetUserId));
            Assert.Equal(ErrorCodes.StudioMemberNotFound, ex.Code);
        }

        [Fact]
        public async Task ApproveMemberAsync_ValidOwner_ApprovesAndSendsEmail()
        {
            // Arrange
            var studio = new Studio { StudioId = _studioId, OwnerId = _userId, StudioName = "Test Studio" };
            var targetParticipant = new StudioParticipant
            {
                StudioId = _studioId,
                UserId = _targetUserId,
                IsApproved = false
            };
            var targetUser = new User
            {
                UserId = _targetUserId,
                FirstName = "Jane",
                LastName = "Doe",
                Email = "jane@test.com",
                Language = "en"
            };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioParticipantRepoMock.Setup(x => x.GetPendingByStudioAndUserAsync(_studioId, _targetUserId))
                .ReturnsAsync(targetParticipant);
            _userRepoMock.Setup(x => x.GetByIdAsync(_targetUserId)).ReturnsAsync(targetUser);

            // Act
            var result = await _service.ApproveMemberAsync(_userId, _studioId, _targetUserId);

            // Assert
            Assert.True(result.IsApproved);
            Assert.Equal("Jane Doe", result.UserName);
            _studioParticipantRepoMock.Verify(x => x.UpdateAsync(targetParticipant), Times.Once);
            _emailServiceMock.Verify(x => x.SendLinkAsync(
                "jane@test.com",
                "Join studio request approved",
                It.IsAny<string>()), Times.Once);
        }

        #endregion

        #region ToggleArchiveStudioAsync

        [Fact]
        public async Task ToggleArchiveStudioAsync_StudioNotFound_ThrowsNotFound()
        {
            // Arrange
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId))
                .ReturnsAsync((Studio?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ToggleArchiveStudioAsync(_userId, _studioId, true));
            Assert.Equal(ErrorCodes.StudioNotFound, ex.Code);
        }

        [Fact]
        public async Task ToggleArchiveStudioAsync_NotOwner_ThrowsForbidden()
        {
            // Arrange
            var studio = new Studio { StudioId = _studioId, OwnerId = _ownerId };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ToggleArchiveStudioAsync(_userId, _studioId, true));
            Assert.Equal(ErrorCodes.AuthForbidden, ex.Code);
        }

        [Fact]
        public async Task ToggleArchiveStudioAsync_Archive_ArchivesStudioAndGroups()
        {
            // Arrange
            var studio = new Studio
            {
                StudioId = _studioId,
                OwnerId = _userId,
                IsArchived = false
            };
            var group = new Group
            {
                GroupId = _groupId,
                StudioId = _studioId,
                IsArchived = false
            };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _groupRepoMock.Setup(x => x.GetStudioGroupsAsync(_studioId))
                .ReturnsAsync(new List<Group> { group });

            // Act
            var result = await _service.ToggleArchiveStudioAsync(_userId, _studioId, true);

            // Assert
            Assert.True(result.IsArchived);
            Assert.True(group.IsArchived);
            _studioRepoMock.Verify(x => x.UpdateStudioAsync(It.IsAny<Studio>()), Times.Once);
            _groupRepoMock.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task ToggleArchiveStudioAsync_Unarchive_UnarchivesStudioAndGroups()
        {
            // Arrange
            var studio = new Studio
            {
                StudioId = _studioId,
                OwnerId = _userId,
                IsArchived = true
            };
            var group = new Group
            {
                GroupId = _groupId,
                StudioId = _studioId,
                IsArchived = true
            };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _groupRepoMock.Setup(x => x.GetStudioGroupsAsync(_studioId))
                .ReturnsAsync(new List<Group> { group });

            // Act
            var result = await _service.ToggleArchiveStudioAsync(_userId, _studioId, false);

            // Assert
            Assert.False(result.IsArchived);
            Assert.False(group.IsArchived);
        }

        #endregion
    }
}
