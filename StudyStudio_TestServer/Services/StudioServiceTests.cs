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
    /// <summary>
    /// Unit tests cho StudioService.
    /// Tests: studio CRUD, member management, permissions, archive operations.
    /// Ref: Services/StudioService.cs
    /// </summary>
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

        /// <summary>
        /// Branch: không có studio nào → trả về danh sách rỗng + Free plan (3 studios)
        /// Ref: StudioService.GetUserStudiosAsync
        /// </summary>
        [Fact]
        public async Task GetUserStudiosAsync_NoStudios_ReturnsEmptyList()
        {
            _subscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync((SubscriptionPlan?)null);
            _studioRepoMock.Setup(x => x.CountStudioCreatedByUserAsync(_userId)).ReturnsAsync(0);
            _studioRepoMock.Setup(x => x.GetByOwnerIdAsync(_userId))
                .ReturnsAsync(new List<Studio>());
            _studioParticipantRepoMock.Setup(x => x.GetStudiosByUserIdAsync(_userId))
                .ReturnsAsync(new List<StudioParticipant>());

            var result = await _service.GetUserStudiosAsync(_userId);

            Assert.NotNull(result);
            Assert.Empty(result.Studios);
            Assert.Equal(3, result.Subscription.StudioLimit);
            Assert.Equal(0, result.Subscription.StudioCreated);
        }

        /// <summary>
        /// Branch: có studio do user sở hữu → trả về studio với role Owner
        /// Ref: StudioService.GetUserStudiosAsync
        /// </summary>
        [Fact]
        public async Task GetUserStudiosAsync_WithOwnedStudios_ReturnsStudios()
        {
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

            var result = await _service.GetUserStudiosAsync(_userId);

            Assert.Single(result.Studios);
            Assert.Equal("My Studio", result.Studios[0].StudioName);
            Assert.Equal(StudioRole.Owner, result.Studios[0].StudioRole);
            Assert.Equal(5, result.Studios[0].GroupCount);
            Assert.Equal(3, result.Studios[0].MemberCount);
        }

        /// <summary>
        /// Branch: có studio mà user là member (không phải owner) → trả về studio với role Member
        /// Ref: StudioService.GetUserStudiosAsync
        /// </summary>
        [Fact]
        public async Task GetUserStudiosAsync_WithMemberStudios_ReturnsStudios()
        {
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

            var result = await _service.GetUserStudiosAsync(_userId);

            Assert.Single(result.Studios);
            Assert.Equal("Member Studio", result.Studios[0].StudioName);
            Assert.Equal(StudioRole.Member, result.Studios[0].StudioRole);
            Assert.True(result.Studios[0].IsMember);
        }

        #endregion

        #region CreateStudioAsync

        /// <summary>
        /// Branch: đã đạt giới hạn studio → throw AppException(StudioLimitReached, 403)
        /// Ref: StudioService.CreateStudioAsync:limit check
        /// </summary>
        [Fact]
        public async Task CreateStudioAsync_StudioLimitReached_ThrowsForbidden()
        {
            var plan = new SubscriptionPlan { MaxStudios = 1 };
            _subscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_ownerId))
                .ReturnsAsync(plan);
            _studioRepoMock.Setup(x => x.CountStudioCreatedByUserAsync(_ownerId)).ReturnsAsync(1);

            var request = new CreateStudioRequest { StudioName = "New Studio" };

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.CreateStudioAsync(_ownerId, request));
            Assert.Equal(ErrorCodes.StudioLimitReached, ex.Code);
            Assert.Equal(403, ex.HttpStatus);
        }

        /// <summary>
        /// Branch: EndDate < StartDate → throw AppException(StudioInvalidDateRange)
        /// Ref: StudioService.CreateStudioAsync:date validation
        /// </summary>
        [Fact]
        public async Task CreateStudioAsync_InvalidDateRange_ThrowsBadRequest()
        {
            _subscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_ownerId))
                .ReturnsAsync((SubscriptionPlan?)null);
            _studioRepoMock.Setup(x => x.CountStudioCreatedByUserAsync(_ownerId)).ReturnsAsync(0);

            var request = new CreateStudioRequest
            {
                StudioName = "Test Studio",
                StartDate = new DateTime(2026, 4, 20),
                EndDate = new DateTime(2026, 4, 10)
            };

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.CreateStudioAsync(_ownerId, request));
            Assert.Equal(ErrorCodes.StudioInvalidDateRange, ex.Code);
        }

        /// <summary>
        /// Branch: StudioName > 255 chars → throw AppException(StudioNameInvalid)
        /// Ref: StudioService.CreateStudioAsync:name length validation
        /// </summary>
        [Fact]
        public async Task CreateStudioAsync_NameTooLong_ThrowsBadRequest()
        {
            _subscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_ownerId))
                .ReturnsAsync((SubscriptionPlan?)null);
            _studioRepoMock.Setup(x => x.CountStudioCreatedByUserAsync(_ownerId)).ReturnsAsync(0);

            var request = new CreateStudioRequest
            {
                StudioName = new string('A', 256)
            };

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.CreateStudioAsync(_ownerId, request));
            Assert.Equal(ErrorCodes.StudioNameInvalid, ex.Code);
        }

        /// <summary>
        /// Branch: Description > 500 chars → throw AppException(StudioDescriptionInvalid)
        /// Ref: StudioService.CreateStudioAsync:description length validation
        /// </summary>
        [Fact]
        public async Task CreateStudioAsync_DescriptionTooLong_ThrowsBadRequest()
        {
            _subscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_ownerId))
                .ReturnsAsync((SubscriptionPlan?)null);
            _studioRepoMock.Setup(x => x.CountStudioCreatedByUserAsync(_ownerId)).ReturnsAsync(0);

            var request = new CreateStudioRequest
            {
                StudioName = "Test Studio",
                Description = new string('A', 501)
            };

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.CreateStudioAsync(_ownerId, request));
            Assert.Equal(ErrorCodes.StudioDescriptionInvalid, ex.Code);
        }

        /// <summary>
        /// Branch: StudioName đã tồn tại với owner → throw AppException(StudioNameAlreadyExist)
        /// Ref: StudioService.CreateStudioAsync:name exists check
        /// </summary>
        [Fact]
        public async Task CreateStudioAsync_NameAlreadyExists_ThrowsBadRequest()
        {
            _subscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_ownerId))
                .ReturnsAsync((SubscriptionPlan?)null);
            _studioRepoMock.Setup(x => x.CountStudioCreatedByUserAsync(_ownerId)).ReturnsAsync(0);
            _studioRepoMock.Setup(x => x.IsStudioNameExistByOwnerIdAsync("Existing Studio", _ownerId))
                .ReturnsAsync(true);

            var request = new CreateStudioRequest { StudioName = "Existing Studio" };

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.CreateStudioAsync(_ownerId, request));
            Assert.Equal(ErrorCodes.StudioNameAlreadyExist, ex.Code);
        }

        /// <summary>
        /// Branch: valid request → tạo studio + tạo participant cho owner
        /// Ref: StudioService.CreateStudioAsync:success path
        /// </summary>
        [Fact]
        public async Task CreateStudioAsync_ValidRequest_CreatesStudioAndParticipant()
        {
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

            var result = await _service.CreateStudioAsync(_ownerId, request);

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

        /// <summary>
        /// Branch: studio không tồn tại → throw AppException(StudioNotFound)
        /// Ref: StudioService.GetStudioDetailAsync:studio exists check
        /// </summary>
        [Fact]
        public async Task GetStudioDetailAsync_StudioNotFound_ThrowsNotFound()
        {
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId))
                .ReturnsAsync((Studio?)null);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.GetStudioDetailAsync(_userId, _studioId));
            Assert.Equal(ErrorCodes.StudioNotFound, ex.Code);
        }

        /// <summary>
        /// Branch: user không phải owner và không phải member → throw AppException(AuthForbidden)
        /// Ref: StudioService.GetStudioDetailAsync:permission check
        /// </summary>
        [Fact]
        public async Task GetStudioDetailAsync_UserNotOwnerNorMember_ThrowsForbidden()
        {
            var studio = new Studio { StudioId = _studioId, OwnerId = _ownerId };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioParticipantRepoMock.Setup(x => x.GetByStudioAndUserAsync(_studioId, _userId))
                .ReturnsAsync((StudioParticipant?)null);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.GetStudioDetailAsync(_userId, _studioId));
            Assert.Equal(ErrorCodes.AuthForbidden, ex.Code);
        }

        /// <summary>
        /// Branch: user là owner → trả về chi tiết studio với role Owner
        /// Ref: StudioService.GetStudioDetailAsync:owner path
        /// </summary>
        [Fact]
        public async Task GetStudioDetailAsync_Owner_ReturnsStudioDetail()
        {
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

            var result = await _service.GetStudioDetailAsync(_userId, _studioId);

            Assert.Equal("Owner Studio", result.StudioName);
            Assert.Equal(StudioRole.Owner, result.StudioRole);
            Assert.Equal(7, result.GroupCount);
            Assert.Equal(5, result.MemberCount);
        }

        /// <summary>
        /// Branch: user là member → chỉ thấy groups mà mình tham gia
        /// Ref: StudioService.GetStudioDetailAsync:member path
        /// </summary>
        [Fact]
        public async Task GetStudioDetailAsync_Member_SeesOnlyTheirGroups()
        {
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

            var result = await _service.GetStudioDetailAsync(_userId, _studioId);

            Assert.Equal(StudioRole.Member, result.StudioRole);
            Assert.Equal(1, result.GroupCount);
        }

        #endregion

        #region DeleteStudioAsync

        /// <summary>
        /// Branch: studio không tồn tại → throw AppException(StudioNotFound)
        /// Ref: StudioService.DeleteStudioAsync:studio exists check
        /// </summary>
        [Fact]
        public async Task DeleteStudioAsync_StudioNotFound_ThrowsNotFound()
        {
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId))
                .ReturnsAsync((Studio?)null);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.DeleteStudioAsync(_userId, _studioId));
            Assert.Equal(ErrorCodes.StudioNotFound, ex.Code);
        }

        /// <summary>
        /// Branch: user không phải owner → throw AppException(AuthForbidden)
        /// Ref: StudioService.DeleteStudioAsync:owner check
        /// </summary>
        [Fact]
        public async Task DeleteStudioAsync_NotOwner_ThrowsForbidden()
        {
            var studio = new Studio { StudioId = _studioId, OwnerId = _ownerId };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.DeleteStudioAsync(_userId, _studioId));
            Assert.Equal(ErrorCodes.AuthForbidden, ex.Code);
        }

        /// <summary>
        /// Branch: user là owner → xóa studio + các groups
        /// Ref: StudioService.DeleteStudioAsync:success path
        /// </summary>
        [Fact]
        public async Task DeleteStudioAsync_Owner_DeletesStudioAndGroups()
        {
            var studio = new Studio { StudioId = _studioId, OwnerId = _userId };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _groupRepoMock.Setup(x => x.GetStudioGroupsAsync(_studioId))
                .ReturnsAsync(new List<Group> { new Group { GroupId = _groupId, StudioId = _studioId } });

            await _service.DeleteStudioAsync(_userId, _studioId);

            _groupRepoMock.Verify(x => x.SaveChangesAsync(), Times.Once);
            _studioRepoMock.Verify(x => x.DeleteStudioAsync(studio), Times.Once);
        }

        #endregion

        #region UpdateStudioAsync

        /// <summary>
        /// Branch: studio không tồn tại → throw AppException(StudioNotFound)
        /// Ref: StudioService.UpdateStudioAsync:studio exists check
        /// </summary>
        [Fact]
        public async Task UpdateStudioAsync_StudioNotFound_ThrowsNotFound()
        {
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId))
                .ReturnsAsync((Studio?)null);

            var request = new UpdateStudioRequest { Id = _studioId, StudioName = "Updated" };

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateStudioAsync(_userId, request));
            Assert.Equal(ErrorCodes.StudioNotFound, ex.Code);
        }

        /// <summary>
        /// Branch: user không phải owner → throw AppException(AuthForbidden)
        /// Ref: StudioService.UpdateStudioAsync:owner check
        /// </summary>
        [Fact]
        public async Task UpdateStudioAsync_NotOwner_ThrowsForbidden()
        {
            var studio = new Studio { StudioId = _studioId, OwnerId = _ownerId };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);

            var request = new UpdateStudioRequest { Id = _studioId, StudioName = "Updated" };

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateStudioAsync(_userId, request));
            Assert.Equal(ErrorCodes.AuthForbidden, ex.Code);
        }

        /// <summary>
        /// Branch: ColorHex không hợp lệ → throw AppException(ValidationInvalidColor)
        /// Ref: StudioService.UpdateStudioAsync:color validation
        /// </summary>
        [Fact]
        public async Task UpdateStudioAsync_InvalidColor_ThrowsBadRequest()
        {
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

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateStudioAsync(_userId, request));
            Assert.Equal(ErrorCodes.ValidationInvalidColor, ex.Code);
        }

        /// <summary>
        /// Branch: BannerUrl không hợp lệ → throw AppException(ValidationInvalidBannerUrl)
        /// Ref: StudioService.UpdateStudioAsync:banner URL validation
        /// </summary>
        [Fact]
        public async Task UpdateStudioAsync_InvalidBannerUrl_ThrowsBadRequest()
        {
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

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateStudioAsync(_userId, request));
            Assert.Equal(ErrorCodes.ValidationInvalidBannerUrl, ex.Code);
        }

        /// <summary>
        /// Branch: Alias chứa ký tự không hợp lệ → throw AppException(ValidationInvalidAlias)
        /// Ref: StudioService.UpdateStudioAsync:alias validation
        /// </summary>
        [Fact]
        public async Task UpdateStudioAsync_InvalidAlias_ThrowsBadRequest()
        {
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
                Alias = "invalid alias with spaces!"
            };

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateStudioAsync(_userId, request));
            Assert.Equal(ErrorCodes.ValidationInvalidAlias, ex.Code);
        }

        /// <summary>
        /// Branch: Alias > 50 chars → throw AppException(ValidationStringLength)
        /// Ref: StudioService.UpdateStudioAsync:alias length validation
        /// </summary>
        [Fact]
        public async Task UpdateStudioAsync_AliasTooLong_ThrowsBadRequest()
        {
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
                Alias = new string('A', 51)
            };

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateStudioAsync(_userId, request));
            Assert.Equal(ErrorCodes.ValidationStringLength, ex.Code);
        }

        /// <summary>
        /// Branch: valid request → cập nhật studio + invalidate cache
        /// Ref: StudioService.UpdateStudioAsync:success path
        /// </summary>
        [Fact]
        public async Task UpdateStudioAsync_ValidRequest_UpdatesAndInvalidatesCache()
        {
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

            var result = await _service.UpdateStudioAsync(_userId, request);

            Assert.Equal("New Studio", result.StudioName);
            Assert.Equal("New description", result.Description);
            Assert.Equal("#FF0000", result.ColorHex);
            Assert.True(result.IsOpen);
            _studioRepoMock.Verify(x => x.UpdateStudioAsync(It.IsAny<Studio>()), Times.Once);
            _cacheServiceMock.Verify(x => x.InvalidateAIStudioCacheAsync(_studioId), Times.Once);
        }

        #endregion

        #region GetStudioMembersAsync

        /// <summary>
        /// Branch: user không phải member → throw AppException(AuthForbidden)
        /// Ref: StudioService.GetStudioMembersAsync:member check
        /// </summary>
        [Fact]
        public async Task GetStudioMembersAsync_UserNotMember_ThrowsForbidden()
        {
            _studioParticipantRepoMock.Setup(x => x.GetByStudioAndUserAsync(_studioId, _userId))
                .ReturnsAsync((StudioParticipant?)null);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.GetStudioMembersAsync(_userId, _studioId));
            Assert.Equal(ErrorCodes.AuthForbidden, ex.Code);
        }

        /// <summary>
        /// Branch: user là member hợp lệ → trả về danh sách members
        /// Ref: StudioService.GetStudioMembersAsync:success path
        /// </summary>
        [Fact]
        public async Task GetStudioMembersAsync_ValidMember_ReturnsMembers()
        {
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

            var result = await _service.GetStudioMembersAsync(_userId, _studioId);

            Assert.Single(result);
            Assert.Equal(_userId, result[0].UserId);
            Assert.Equal(StudioRole.Member, result[0].StudioRole);
        }

        #endregion

        #region LeaveStudioAsync

        /// <summary>
        /// Branch: studio không tồn tại → throw AppException(StudioNotFound)
        /// Ref: StudioService.LeaveStudioAsync:studio exists check
        /// </summary>
        [Fact]
        public async Task LeaveStudioAsync_StudioNotFound_ThrowsNotFound()
        {
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId))
                .ReturnsAsync((Studio?)null);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.LeaveStudioAsync(_userId, _studioId));
            Assert.Equal(ErrorCodes.StudioNotFound, ex.Code);
        }

        /// <summary>
        /// Branch: user không phải member → throw AppException(StudioNotFound)
        /// Ref: StudioService.LeaveStudioAsync:member check
        /// </summary>
        [Fact]
        public async Task LeaveStudioAsync_UserNotMember_ThrowsNotFound()
        {
            var studio = new Studio { StudioId = _studioId, OwnerId = _ownerId };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioParticipantRepoMock.Setup(x => x.GetByStudioAndUserIncludeNonApprovedAsync(_studioId, _userId))
                .ReturnsAsync((StudioParticipant?)null);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.LeaveStudioAsync(_userId, _studioId));
            Assert.Equal(ErrorCodes.StudioNotFound, ex.Code);
        }

        /// <summary>
        /// Branch: owner không thể leave → throw AppException(StudioCannotLeaveAsOwner)
        /// Ref: StudioService.LeaveStudioAsync:owner cannot leave
        /// </summary>
        [Fact]
        public async Task LeaveStudioAsync_OwnerCannotLeave_ThrowsForbidden()
        {
            var studio = new Studio { StudioId = _studioId, OwnerId = _userId };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioParticipantRepoMock.Setup(x => x.GetByStudioAndUserIncludeNonApprovedAsync(_studioId, _userId))
                .ReturnsAsync(new StudioParticipant { StudioId = _studioId, UserId = _userId, Role = StudioRole.Owner });

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.LeaveStudioAsync(_userId, _studioId));
            Assert.Equal(ErrorCodes.StudioCannotLeaveAsOwner, ex.Code);
        }

        /// <summary>
        /// Branch: member hợp lệ → leave studio + leave các groups
        /// Ref: StudioService.LeaveStudioAsync:success path
        /// </summary>
        [Fact]
        public async Task LeaveStudioAsync_ValidMember_LeavesStudioAndGroups()
        {
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

            var result = await _service.LeaveStudioAsync(_userId, _studioId);

            Assert.Equal(_studioId, result.StudioId);
            Assert.Equal("Test Studio", result.StudioName);
            _studioParticipantRepoMock.Verify(x => x.RemoveAsync(participant), Times.Once);
            _groupParticipantRepoMock.Verify(x => x.RemoveRangeAsync(It.IsAny<List<GroupParticipant>>()), Times.Once);
        }

        #endregion

        #region ToggleIsOpenAsync

        /// <summary>
        /// Branch: studio không tồn tại → throw AppException(StudioNotFound)
        /// Ref: StudioService.ToggleIsOpenAsync:studio exists check
        /// </summary>
        [Fact]
        public async Task ToggleIsOpenAsync_StudioNotFound_ThrowsNotFound()
        {
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId))
                .ReturnsAsync((Studio?)null);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ToggleIsOpenAsync(_userId, _studioId, true));
            Assert.Equal(ErrorCodes.StudioNotFound, ex.Code);
        }

        /// <summary>
        /// Branch: user không phải owner → throw AppException(AuthForbidden)
        /// Ref: StudioService.ToggleIsOpenAsync:owner check
        /// </summary>
        [Fact]
        public async Task ToggleIsOpenAsync_NotOwner_ThrowsForbidden()
        {
            var studio = new Studio { StudioId = _studioId, OwnerId = _ownerId };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ToggleIsOpenAsync(_userId, _studioId, true));
            Assert.Equal(ErrorCodes.AuthForbidden, ex.Code);
        }

        /// <summary>
        /// Branch: owner toggle isOpen → cập nhật + invalidate cache
        /// Ref: StudioService.ToggleIsOpenAsync:success path
        /// </summary>
        [Fact]
        public async Task ToggleIsOpenAsync_Owner_UpdatesAndInvalidatesCache()
        {
            var studio = new Studio
            {
                StudioId = _studioId,
                OwnerId = _userId,
                StudioName = "Test Studio",
                IsOpen = false
            };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);

            var result = await _service.ToggleIsOpenAsync(_userId, _studioId, true);

            Assert.True(result.IsOpen);
            _studioRepoMock.Verify(x => x.UpdateStudioAsync(It.IsAny<Studio>()), Times.Once);
            _cacheServiceMock.Verify(x => x.InvalidateAIStudioCacheAsync(_studioId), Times.Once);
        }

        #endregion

        #region GetPendingMembersAsync

        /// <summary>
        /// Branch: studio không tồn tại → throw AppException(StudioNotFound)
        /// Ref: StudioService.GetPendingMembersAsync:studio exists check
        /// </summary>
        [Fact]
        public async Task GetPendingMembersAsync_StudioNotFound_ThrowsNotFound()
        {
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId))
                .ReturnsAsync((Studio?)null);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.GetPendingMembersAsync(_userId, _studioId));
            Assert.Equal(ErrorCodes.StudioNotFound, ex.Code);
        }

        /// <summary>
        /// Branch: user không phải owner → throw AppException(AuthForbidden)
        /// Ref: StudioService.GetPendingMembersAsync:owner check
        /// </summary>
        [Fact]
        public async Task GetPendingMembersAsync_NotOwner_ThrowsForbidden()
        {
            var studio = new Studio { StudioId = _studioId, OwnerId = _ownerId };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.GetPendingMembersAsync(_userId, _studioId));
            Assert.Equal(ErrorCodes.AuthForbidden, ex.Code);
        }

        /// <summary>
        /// Branch: owner xem pending members → trả về danh sách
        /// Ref: StudioService.GetPendingMembersAsync:success path
        /// </summary>
        [Fact]
        public async Task GetPendingMembersAsync_Owner_ReturnsPendingMembers()
        {
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

            var result = await _service.GetPendingMembersAsync(_userId, _studioId);

            Assert.Equal(_studioId, result.StudioId);
            Assert.Equal(1, result.TotalPending);
            Assert.Single(result.PendingMembers);
        }

        #endregion

        #region RemoveMemberAsync

        /// <summary>
        /// Branch: studio không tồn tại → throw AppException(StudioNotFound)
        /// Ref: StudioService.RemoveMemberAsync:studio exists check
        /// </summary>
        [Fact]
        public async Task RemoveMemberAsync_StudioNotFound_ThrowsNotFound()
        {
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId))
                .ReturnsAsync((Studio?)null);

            var request = new RemoveStudioMemberRequest { StudioId = _studioId, UserId = _targetUserId };

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.RemoveMemberAsync(_userId, request));
            Assert.Equal(ErrorCodes.StudioNotFound, ex.Code);
        }

        /// <summary>
        /// Branch: user không phải owner → throw AppException(AuthForbidden)
        /// Ref: StudioService.RemoveMemberAsync:owner check
        /// </summary>
        [Fact]
        public async Task RemoveMemberAsync_NotOwner_ThrowsForbidden()
        {
            var studio = new Studio { StudioId = _studioId, OwnerId = _ownerId };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);

            var request = new RemoveStudioMemberRequest { StudioId = _studioId, UserId = _targetUserId };

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.RemoveMemberAsync(_userId, request));
            Assert.Equal(ErrorCodes.AuthForbidden, ex.Code);
        }

        /// <summary>
        /// Branch: owner tự xóa mình → throw AppException(StudioCannotRemoveSelf)
        /// Ref: StudioService.RemoveMemberAsync:cannot remove self
        /// </summary>
        [Fact]
        public async Task RemoveMemberAsync_RemoveSelf_ThrowsBadRequest()
        {
            var studio = new Studio { StudioId = _studioId, OwnerId = _userId };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);

            var request = new RemoveStudioMemberRequest { StudioId = _studioId, UserId = _userId };

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.RemoveMemberAsync(_userId, request));
            Assert.Equal(ErrorCodes.StudioCannotRemoveSelf, ex.Code);
        }

        /// <summary>
        /// Branch: member không tồn tại → throw AppException(StudioMemberNotFound)
        /// Ref: StudioService.RemoveMemberAsync:member exists check
        /// </summary>
        [Fact]
        public async Task RemoveMemberAsync_MemberNotFound_ThrowsNotFound()
        {
            var studio = new Studio { StudioId = _studioId, OwnerId = _userId };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioParticipantRepoMock.Setup(x => x.GetByStudioAndUserTrackedAsync(_studioId, _targetUserId))
                .ReturnsAsync((StudioParticipant?)null);

            var request = new RemoveStudioMemberRequest { StudioId = _studioId, UserId = _targetUserId };

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.RemoveMemberAsync(_userId, request));
            Assert.Equal(ErrorCodes.StudioMemberNotFound, ex.Code);
        }

        /// <summary>
        /// Branch: xóa owner khác → throw AppException(StudioCannotRemoveOwner)
        /// Ref: StudioService.RemoveMemberAsync:cannot remove owner
        /// </summary>
        [Fact]
        public async Task RemoveMemberAsync_RemoveOwner_ThrowsBadRequest()
        {
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

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.RemoveMemberAsync(_userId, request));
            Assert.Equal(ErrorCodes.StudioCannotRemoveOwner, ex.Code);
        }

        /// <summary>
        /// Branch: xóa member hợp lệ → xóa + invalidate cache
        /// Ref: StudioService.RemoveMemberAsync:success path
        /// </summary>
        [Fact]
        public async Task RemoveMemberAsync_ValidMember_RemovesAndInvalidatesCache()
        {
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

            var result = await _service.RemoveMemberAsync(_userId, request);

            Assert.Equal(_targetUserId, result.RemovedUserId);
            Assert.Equal("Jane Doe", result.RemovedUserName);
            _studioParticipantRepoMock.Verify(x => x.RemoveAsync(targetMember), Times.Once);
        }

        #endregion

        #region ApproveMemberAsync

        /// <summary>
        /// Branch: studio không tồn tại → throw AppException(StudioNotFound)
        /// Ref: StudioService.ApproveMemberAsync:studio exists check
        /// </summary>
        [Fact]
        public async Task ApproveMemberAsync_StudioNotFound_ThrowsNotFound()
        {
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId))
                .ReturnsAsync((Studio?)null);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ApproveMemberAsync(_userId, _studioId, _targetUserId));
            Assert.Equal(ErrorCodes.StudioNotFound, ex.Code);
        }

        /// <summary>
        /// Branch: user không phải owner → throw AppException(AuthForbidden)
        /// Ref: StudioService.ApproveMemberAsync:owner check
        /// </summary>
        [Fact]
        public async Task ApproveMemberAsync_NotOwner_ThrowsForbidden()
        {
            var studio = new Studio { StudioId = _studioId, OwnerId = _ownerId };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ApproveMemberAsync(_userId, _studioId, _targetUserId));
            Assert.Equal(ErrorCodes.AuthForbidden, ex.Code);
        }

        /// <summary>
        /// Branch: pending member không tồn tại → throw AppException(StudioMemberNotFound)
        /// Ref: StudioService.ApproveMemberAsync:pending member check
        /// </summary>
        [Fact]
        public async Task ApproveMemberAsync_PendingMemberNotFound_ThrowsNotFound()
        {
            var studio = new Studio { StudioId = _studioId, OwnerId = _userId };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);
            _studioParticipantRepoMock.Setup(x => x.GetPendingByStudioAndUserAsync(_studioId, _targetUserId))
                .ReturnsAsync((StudioParticipant?)null);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ApproveMemberAsync(_userId, _studioId, _targetUserId));
            Assert.Equal(ErrorCodes.StudioMemberNotFound, ex.Code);
        }

        /// <summary>
        /// Branch: owner approve pending member → approve + gửi email
        /// Ref: StudioService.ApproveMemberAsync:success path
        /// </summary>
        [Fact]
        public async Task ApproveMemberAsync_ValidOwner_ApprovesAndSendsEmail()
        {
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

            var result = await _service.ApproveMemberAsync(_userId, _studioId, _targetUserId);

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

        /// <summary>
        /// Branch: studio không tồn tại → throw AppException(StudioNotFound)
        /// Ref: StudioService.ToggleArchiveStudioAsync:studio exists check
        /// </summary>
        [Fact]
        public async Task ToggleArchiveStudioAsync_StudioNotFound_ThrowsNotFound()
        {
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId))
                .ReturnsAsync((Studio?)null);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ToggleArchiveStudioAsync(_userId, _studioId, true));
            Assert.Equal(ErrorCodes.StudioNotFound, ex.Code);
        }

        /// <summary>
        /// Branch: user không phải owner → throw AppException(AuthForbidden)
        /// Ref: StudioService.ToggleArchiveStudioAsync:owner check
        /// </summary>
        [Fact]
        public async Task ToggleArchiveStudioAsync_NotOwner_ThrowsForbidden()
        {
            var studio = new Studio { StudioId = _studioId, OwnerId = _ownerId };
            _studioRepoMock.Setup(x => x.GetByIdAsync(_studioId)).ReturnsAsync(studio);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ToggleArchiveStudioAsync(_userId, _studioId, true));
            Assert.Equal(ErrorCodes.AuthForbidden, ex.Code);
        }

        /// <summary>
        /// Branch: archive=true → archive studio + các groups
        /// Ref: StudioService.ToggleArchiveStudioAsync:archive path
        /// </summary>
        [Fact]
        public async Task ToggleArchiveStudioAsync_Archive_ArchivesStudioAndGroups()
        {
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

            var result = await _service.ToggleArchiveStudioAsync(_userId, _studioId, true);

            Assert.True(result.IsArchived);
            Assert.True(group.IsArchived);
            _studioRepoMock.Verify(x => x.UpdateStudioAsync(It.IsAny<Studio>()), Times.Once);
            _groupRepoMock.Verify(x => x.SaveChangesAsync(), Times.Once);
        }

        /// <summary>
        /// Branch: archive=false → unarchive studio + các groups
        /// Ref: StudioService.ToggleArchiveStudioAsync:unarchive path
        /// </summary>
        [Fact]
        public async Task ToggleArchiveStudioAsync_Unarchive_UnarchivesStudioAndGroups()
        {
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

            var result = await _service.ToggleArchiveStudioAsync(_userId, _studioId, false);

            Assert.False(result.IsArchived);
            Assert.False(group.IsArchived);
        }

        #endregion
    }
}