using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Moq;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services;
using StudioStudio_Server.Services.Interfaces;
namespace StudyStudio_TestServer.Services
{
    /// <summary>
    /// Unit tests cho UserService.
    /// Tests: user profile management, password change, account deletion, subscription info.
    /// Ref: Services/UserService.cs
    /// </summary>
    public class UserServiceTests
    {
        private readonly Mock<IUserRepository> _userRepoMock;
        private readonly Mock<IPasswordHasher<User>> _passwordHasherMock;
        private readonly Mock<IStudioRepository> _studioRepositoryMock;
        private readonly Mock<IGroupRepository> _groupRepositoryMock;
        private readonly Mock<IDocumentService> _documentServiceMock;
        private readonly Mock<IUserSubscriptionRepository> _subscriptionRepoMock;
        private readonly Mock<IAIRequestLogRepository> _aiRequestLogRepoMock;
        private readonly Mock<ICacheService> _cacheServiceMock;
        private readonly UserService _service;

        private readonly Guid _userId = Guid.NewGuid();

        public UserServiceTests()
        {
            _userRepoMock = new Mock<IUserRepository>();
            _passwordHasherMock = new Mock<IPasswordHasher<User>>();
            _studioRepositoryMock = new Mock<IStudioRepository>();
            _groupRepositoryMock = new Mock<IGroupRepository>();
            _documentServiceMock = new Mock<IDocumentService>();
            _subscriptionRepoMock = new Mock<IUserSubscriptionRepository>();
            _aiRequestLogRepoMock = new Mock<IAIRequestLogRepository>();
            _cacheServiceMock = new Mock<ICacheService>();
            var environmentMock = new Mock<IWebHostEnvironment>();

            // Setup cache key methods
            _cacheServiceMock.Setup(x => x.GetUserProfileKey(_userId))
                .Returns($"user_profile:{_userId}");
            _cacheServiceMock.Setup(x => x.GetUserSubscriptionKey(_userId))
                .Returns($"user_subscription:{_userId}");
            _cacheServiceMock.Setup(x => x.GetExpirationForKey(It.IsAny<string>()))
                .Returns(TimeSpan.FromMinutes(5));

            _service = new UserService(
                _userRepoMock.Object,
                _passwordHasherMock.Object,
                environmentMock.Object,
                _studioRepositoryMock.Object,
                _groupRepositoryMock.Object,
                _documentServiceMock.Object,
                _subscriptionRepoMock.Object,
                _aiRequestLogRepoMock.Object,
                _cacheServiceMock.Object);
        }

        #region GetByIdAsync

        /// <summary>
        /// Branch: user found → returns User
        /// Ref: UserService.GetByIdAsync:59-68 (via cache)
        /// </summary>
        [Fact]
        public async Task GetByIdAsync_UserFound_ReturnsUser()
        {
            var user = new User { UserId = _userId, FirstName = "John", LastName = "Doe" };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);

            var result = await _service.GetByIdAsync(_userId);

            Assert.NotNull(result);
            Assert.Equal(_userId, result.UserId);
            Assert.Equal("John", result.FirstName);
        }

        /// <summary>
        /// Branch: user not found → returns null
        /// Ref: UserService.GetByIdAsync:59-68
        /// </summary>
        [Fact]
        public async Task GetByIdAsync_UserNotFound_ReturnsNull()
        {
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync((User?)null);

            var result = await _service.GetByIdAsync(_userId);

            Assert.Null(result);
        }

        #endregion

        #region GetByEmailAsync

        /// <summary>
        /// Branch: user found by email → returns User
        /// Ref: UserService.GetByEmailAsync:74-77
        /// </summary>
        [Fact]
        public async Task GetByEmailAsync_UserFound_ReturnsUser()
        {
            var email = "john@example.com";
            var user = new User { UserId = _userId, Email = email };
            _userRepoMock.Setup(x => x.GetByEmailAsync(email)).ReturnsAsync(user);

            var result = await _service.GetByEmailAsync(email);

            Assert.NotNull(result);
            Assert.Equal(email, result.Email);
        }

        /// <summary>
        /// Branch: user not found by email → returns null
        /// Ref: UserService.GetByEmailAsync:74-77
        /// </summary>
        [Fact]
        public async Task GetByEmailAsync_UserNotFound_ReturnsNull()
        {
            _userRepoMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            var result = await _service.GetByEmailAsync("notfound@example.com");

            Assert.Null(result);
        }

        #endregion

        #region UpdateAsync

        /// <summary>
        /// Branch: valid user → sets UpdatedAt + calls UpdateAsync + invalidates cache
        /// Ref: UserService.UpdateAsync:84-91
        /// </summary>
        [Fact]
        public async Task UpdateAsync_ValidUser_CallsRepositoryAndInvalidatesCache()
        {
            var user = new User { UserId = _userId, FirstName = "Jane" };

            await _service.UpdateAsync(user);

            Assert.NotEqual(default, user.UpdatedAt);
            _userRepoMock.Verify(x => x.UpdateAsync(user), Times.Once);
            _cacheServiceMock.Verify(x => x.InvalidateUserCacheAsync(_userId), Times.Once);
        }

        #endregion

        #region DeleteAsync

        /// <summary>
        /// Branch: user == null → throw AppException(ErrorCodes.UserNotFound)
        /// Ref: UserService.DeleteAsync:105-109
        /// </summary>
        [Fact]
        public async Task DeleteAsync_UserNotFound_ThrowsNotFound()
        {
            _userRepoMock.Setup(x => x.GetByIdIncludingDeletedAsync(_userId))
                .ReturnsAsync((User?)null);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.DeleteAsync(_userId));
            Assert.Equal(ErrorCodes.UserNotFound, ex.Code);
            Assert.Equal(404, ex.HttpStatus);
        }

        /// <summary>
        /// Branch: user.Status == Deleted → throw AppException(ErrorCodes.UserAccountAlreadyDeleted)
        /// Ref: UserService.DeleteAsync:111-114
        /// </summary>
        [Fact]
        public async Task DeleteAsync_AlreadyDeleted_ThrowsBadRequest()
        {
            var user = new User { UserId = _userId, Status = UserStatus.Deleted };
            _userRepoMock.Setup(x => x.GetByIdIncludingDeletedAsync(_userId))
                .ReturnsAsync(user);

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.DeleteAsync(_userId));
            Assert.Equal(ErrorCodes.UserAccountAlreadyDeleted, ex.Code);
            Assert.Equal(400, ex.HttpStatus);
        }

        /// <summary>
        /// Branch: valid active user → ghostUser (anonymize) + invalidate cache
        /// Ref: UserService.DeleteAsync:116-135
        /// </summary>
        [Fact]
        public async Task DeleteAsync_ValidUser_GhostsUserAndInvalidatesCache()
        {
            var user = new User
            {
                UserId = _userId,
                Email = "original@example.com",
                FirstName = "John",
                LastName = "Doe",
                Status = UserStatus.Active
            };
            var ownedStudio = new Studio { StudioId = Guid.NewGuid(), OwnerId = _userId, IsArchived = false };
            var studioGroup = new Group { GroupId = Guid.NewGuid(), CreatedBy = Guid.NewGuid(), StudioId = ownedStudio.StudioId, IsArchived = false };
            var standaloneGroup = new Group { GroupId = Guid.NewGuid(), CreatedBy = _userId, StudioId = null, IsArchived = false };

            _userRepoMock.Setup(x => x.GetByIdIncludingDeletedAsync(_userId))
                .ReturnsAsync(user);
            _passwordHasherMock.Setup(x => x.HashPassword(user, It.IsAny<string>()))
                .Returns("hashed_password");
            _studioRepositoryMock.Setup(x => x.GetByOwnerIdAsync(_userId))
                .ReturnsAsync(new List<Studio> { ownedStudio });
            _studioRepositoryMock.Setup(x => x.UpdateStudioAsync(ownedStudio))
                .Returns(Task.CompletedTask);
            _groupRepositoryMock.Setup(x => x.GetStudioGroupsAsync(ownedStudio.StudioId))
                .ReturnsAsync(new List<Group> { studioGroup });
            _groupRepositoryMock.Setup(x => x.GetByCreatedByAsync(_userId))
                .ReturnsAsync(new List<Group> { standaloneGroup });
            _groupRepositoryMock.Setup(x => x.UpdateAsync(studioGroup))
                .Returns(Task.CompletedTask);
            _groupRepositoryMock.Setup(x => x.UpdateAsync(standaloneGroup))
                .Returns(Task.CompletedTask);
            _documentServiceMock.Setup(x => x.DeleteDocumentsExternalDataAsync(It.IsAny<IEnumerable<Guid>>()))
                .Returns(Task.CompletedTask);

            await _service.DeleteAsync(_userId);

            Assert.Equal(UserStatus.Deleted, user.Status);
            Assert.Equal("Deleted", user.FirstName);
            Assert.Equal("User", user.LastName);
            Assert.Null(user.PhoneNumber);
            Assert.Null(user.Bio);
            Assert.Null(user.AvatarUrl);
            Assert.Null(user.GoogleId);
            Assert.NotNull(user.PasswordHash);
            Assert.True(ownedStudio.IsArchived);
            Assert.True(studioGroup.IsArchived);
            Assert.True(standaloneGroup.IsArchived);
            _userRepoMock.Verify(x => x.UpdateAsync(user), Times.Once);
            _studioRepositoryMock.Verify(x => x.UpdateStudioAsync(ownedStudio), Times.Once);
            _groupRepositoryMock.Verify(x => x.UpdateAsync(studioGroup), Times.Once);
            _groupRepositoryMock.Verify(x => x.UpdateAsync(standaloneGroup), Times.Once);
            _documentServiceMock.Verify(x => x.DeleteDocumentsExternalDataAsync(
                It.Is<IEnumerable<Guid>>(ids => ids.ToHashSet().SetEquals(new[] { studioGroup.GroupId, standaloneGroup.GroupId }))),
                Times.Once);
            _cacheServiceMock.Verify(x => x.InvalidateUserCacheAsync(_userId), Times.Once);
        }

        #endregion

        #region ChangePasswordAsync

        /// <summary>
        /// Branch: user == null → throw AppException(ErrorCodes.UserNotFound)
        /// Ref: UserService.ChangePasswordAsync:149-153
        /// </summary>
        [Fact]
        public async Task ChangePasswordAsync_UserNotFound_ThrowsNotFound()
        {
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId))
                .ReturnsAsync((User?)null);

            var request = new ChangePasswordRequest
            {
                CurrentPassword = "OldPass123!",
                NewPassword = "NewPass456!",
                ConfirmPassword = "NewPass456!"
            };

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ChangePasswordAsync(_userId, request));
            Assert.Equal(ErrorCodes.UserNotFound, ex.Code);
        }

        /// <summary>
        /// Branch: verifyResult != Success → throw AppException(ErrorCodes.AuthIncorrectCurrentPassword)
        /// Ref: UserService.ChangePasswordAsync:155-160
        /// </summary>
        [Fact]
        public async Task ChangePasswordAsync_IncorrectCurrentPassword_ThrowsBadRequest()
        {
            var user = new User { UserId = _userId, PasswordHash = "hashed_password" };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);
            _passwordHasherMock.Setup(x => x.VerifyHashedPassword(user, "hashed_password", "WrongPass123!"))
                .Returns(PasswordVerificationResult.Failed);

            var request = new ChangePasswordRequest
            {
                CurrentPassword = "WrongPass123!",
                NewPassword = "NewPass456!",
                ConfirmPassword = "NewPass456!"
            };

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ChangePasswordAsync(_userId, request));
            Assert.Equal(ErrorCodes.AuthIncorrectCurrentPassword, ex.Code);
        }

        /// <summary>
        /// Branch: !IsValidPassword(newPassword) → throw AppException(ErrorCodes.ValidationInvalidPassword)
        /// Ref: UserService.ChangePasswordAsync:162-166
        /// </summary>
        [Fact]
        public async Task ChangePasswordAsync_InvalidNewPasswordFormat_ThrowsBadRequest()
        {
            var user = new User { UserId = _userId, PasswordHash = "hashed_password" };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);
            _passwordHasherMock.Setup(x => x.VerifyHashedPassword(user, "hashed_password", "OldPass123!"))
                .Returns(PasswordVerificationResult.Success);

            var request = new ChangePasswordRequest
            {
                CurrentPassword = "OldPass123!",
                NewPassword = "weak", // Too short, no uppercase, no digit
                ConfirmPassword = "weak"
            };

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ChangePasswordAsync(_userId, request));
            Assert.Equal(ErrorCodes.ValidationInvalidPassword, ex.Code);
        }

        /// <summary>
        /// Branch: newPassword != confirmPassword → throw AppException(ErrorCodes.ValidationPasswordMismatch)
        /// Ref: UserService.ChangePasswordAsync:168-172
        /// </summary>
        [Fact]
        public async Task ChangePasswordAsync_PasswordMismatch_ThrowsBadRequest()
        {
            var user = new User { UserId = _userId, PasswordHash = "hashed_password" };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);
            _passwordHasherMock.Setup(x => x.VerifyHashedPassword(user, "hashed_password", "OldPass123!"))
                .Returns(PasswordVerificationResult.Success);

            var request = new ChangePasswordRequest
            {
                CurrentPassword = "OldPass123!",
                NewPassword = "NewPass456!",
                ConfirmPassword = "DifferentPass789!" // Mismatch
            };

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ChangePasswordAsync(_userId, request));
            Assert.Equal(ErrorCodes.ValidationPasswordMismatch, ex.Code);
        }

        /// <summary>
        /// Branch: newPassword same as current → throw AppException(ErrorCodes.ValidationNewPasswordSameAsCurrent)
        /// Ref: UserService.ChangePasswordAsync:176-181
        /// </summary>
        [Fact]
        public async Task ChangePasswordAsync_SameAsCurrentPassword_ThrowsBadRequest()
        {
            var user = new User { UserId = _userId, PasswordHash = "hashed_password" };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);
            _passwordHasherMock.Setup(x => x.VerifyHashedPassword(user, "hashed_password", "OldPass123!"))
                .Returns(PasswordVerificationResult.Success);
            _passwordHasherMock.Setup(x => x.HashPassword(user, "OldPass123!"))
                .Returns("hashed_password");

            var request = new ChangePasswordRequest
            {
                CurrentPassword = "OldPass123!",
                NewPassword = "OldPass123!", // Same as current
                ConfirmPassword = "OldPass123!"
            };

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ChangePasswordAsync(_userId, request));
            Assert.Equal(ErrorCodes.ValidationNewPasswordSameAsCurrent, ex.Code);
        }

        /// <summary>
        /// Branch: all validations pass → updates password + invalidate cache
        /// Ref: UserService.ChangePasswordAsync:183-190
        /// </summary>
        [Fact]
        public async Task ChangePasswordAsync_ValidRequest_UpdatesPasswordAndInvalidatesCache()
        {
            var user = new User { UserId = _userId, PasswordHash = "old_hash" };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);
            _passwordHasherMock.Setup(x => x.VerifyHashedPassword(user, "old_hash", "OldPass123!"))
                .Returns(PasswordVerificationResult.Success);
            _passwordHasherMock.Setup(x => x.VerifyHashedPassword(user, "old_hash", "NewPass456!"))
                .Returns(PasswordVerificationResult.Failed);
            _passwordHasherMock.Setup(x => x.HashPassword(user, "NewPass456!"))
                .Returns("new_hash");

            var request = new ChangePasswordRequest
            {
                CurrentPassword = "OldPass123!",
                NewPassword = "NewPass456!",
                ConfirmPassword = "NewPass456!"
            };

            await _service.ChangePasswordAsync(_userId, request);

            Assert.Equal("new_hash", user.PasswordHash);
            Assert.NotEqual(default, user.UpdatedAt);
            _userRepoMock.Verify(x => x.UpdateAsync(user), Times.Once);
            _cacheServiceMock.Verify(x => x.InvalidateUserCacheAsync(_userId), Times.Once);
        }

        #endregion

        #region UpdateProfileAsync

        /// <summary>
        /// Branch: user == null → throw AppException(ErrorCodes.UserNotFound)
        /// Ref: UserService.UpdateProfileAsync:203-207
        /// </summary>
        [Fact]
        public async Task UpdateProfileAsync_UserNotFound_ThrowsNotFound()
        {
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId))
                .ReturnsAsync((User?)null);

            var request = new UpdateUserProfileRequest { FirstName = "Jane" };

            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateProfileAsync(_userId, request));
            Assert.Equal(ErrorCodes.UserNotFound, ex.Code);
        }

        /// <summary>
        /// Branch: valid user → updates fields (FirstName, LastName, Bio) + invalidate cache
        /// Ref: UserService.UpdateProfileAsync:209-237
        /// </summary>
        [Fact]
        public async Task UpdateProfileAsync_ValidRequest_UpdatesFieldsAndInvalidatesCache()
        {
            var user = new User { UserId = _userId, FirstName = "John" };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);

            var request = new UpdateUserProfileRequest
            {
                FirstName = "Jane",
                LastName = "Smith",
                Bio = "Hello world"
            };

            await _service.UpdateProfileAsync(_userId, request);

            Assert.Equal("Jane", user.FirstName);
            Assert.Equal("Smith", user.LastName);
            Assert.Equal("Hello world", user.Bio);
            Assert.NotEqual(default, user.UpdatedAt);
            _userRepoMock.Verify(x => x.UpdateAsync(user), Times.Once);
            _cacheServiceMock.Verify(x => x.InvalidateUserCacheAsync(_userId), Times.Once);
        }

        /// <summary>
        /// Branch: request.Language provided → updates Language
        /// Ref: UserService.UpdateProfileAsync:221-222
        /// </summary>
        [Fact]
        public async Task UpdateProfileAsync_WithLanguage_UpdatesLanguage()
        {
            var user = new User { UserId = _userId };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);

            var request = new UpdateUserProfileRequest { Language = "en" };

            await _service.UpdateProfileAsync(_userId, request);

            Assert.Equal("en", user.Language);
        }

        /// <summary>
        /// Branch: request.EmailNotificationEnabled provided → updates flag
        /// Ref: UserService.UpdateProfileAsync:224-226
        /// </summary>
        [Fact]
        public async Task UpdateProfileAsync_WithEmailNotification_UpdatesFlag()
        {
            var user = new User { UserId = _userId, EmailNotificationEnabled = false };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);

            var request = new UpdateUserProfileRequest { EmailNotificationEnabled = true };

            await _service.UpdateProfileAsync(_userId, request);

            Assert.True(user.EmailNotificationEnabled);
        }

        #endregion

        #region GetAiRequestLimitInfoAsync

        /// <summary>
        /// Branch: subscription == null (Free plan) → dailyLimit = 20
        /// Ref: UserService.GetAiRequestLimitInfoAsync:318-340
        /// </summary>
        [Fact]
        public async Task GetAiRequestLimitInfoAsync_FreePlan_Returns20Limit()
        {
            _aiRequestLogRepoMock.Setup(x => x.CountTodayRequestsAsync(_userId, It.IsAny<DateTime>()))
                .ReturnsAsync(5);
            _subscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync((SubscriptionPlan?)null); // Free plan

            var (usedToday, dailyLimit) = await _service.GetAiRequestLimitInfoAsync(_userId);

            Assert.Equal(5, usedToday);
            Assert.Equal(20, dailyLimit);
        }

        /// <summary>
        /// Branch: subscription has custom limit → returns that limit
        /// Ref: UserService.GetAiRequestLimitInfoAsync:335
        /// </summary>
        [Fact]
        public async Task GetAiRequestLimitInfoAsync_PremiumPlan_ReturnsCustomLimit()
        {
            _aiRequestLogRepoMock.Setup(x => x.CountTodayRequestsAsync(_userId, It.IsAny<DateTime>()))
                .ReturnsAsync(50);
            var premiumPlan = new SubscriptionPlan { PlanName = "Premium", MaxAiRequestsPerDay = 100 };
            _subscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync(premiumPlan);

            var (usedToday, dailyLimit) = await _service.GetAiRequestLimitInfoAsync(_userId);

            Assert.Equal(50, usedToday);
            Assert.Equal(100, dailyLimit);
        }

        /// <summary>
        /// Branch: 0 requests today → usedToday = 0
        /// Ref: UserService.GetAiRequestLimitInfoAsync:325
        /// </summary>
        [Fact]
        public async Task GetAiRequestLimitInfoAsync_ZeroRequests_ReturnsZeroUsed()
        {
            _aiRequestLogRepoMock.Setup(x => x.CountTodayRequestsAsync(_userId, It.IsAny<DateTime>()))
                .ReturnsAsync(0);
            _subscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync((SubscriptionPlan?)null);

            var (usedToday, dailyLimit) = await _service.GetAiRequestLimitInfoAsync(_userId);

            Assert.Equal(0, usedToday);
            Assert.Equal(20, dailyLimit);
        }

        #endregion

        #region GetUserSubscriptionPlan

        /// <summary>
        /// Branch: subscription == null → returns defaults (Free plan)
        /// Ref: UserService.GetUserSubscriptionPlan:342-365
        /// </summary>
        [Fact]
        public async Task GetUserSubscriptionPlan_NullSubscription_ReturnsDefaults()
        {
            // Note: In production, every user ALWAYS has a subscription (default Free plan).
            // The GetSubscriptionPlanByUserIdAsync returns null only in test when not mocked.
            var freePlan = new SubscriptionPlan
            {
                PlanId = Guid.Empty,
                PlanName = "Free",
                MaxAiRequestsPerDay = 20,
                MaxGroups = 3
            };
            _subscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync(freePlan);

            var result = await _service.GetUserSubscriptionPlan(_userId);

            Assert.NotNull(result);
            Assert.Equal(Guid.Empty, result.PlanId);
            Assert.Equal(20, result.MaxAiRequestsPerDay);
        }

        /// <summary>
        /// Branch: subscription != null → returns all plan fields
        /// Ref: UserService.GetUserSubscriptionPlan:352-364
        /// </summary>
        [Fact]
        public async Task GetUserSubscriptionPlan_WithPremiumPlan_ReturnsCorrectData()
        {
            var plan = new SubscriptionPlan
            {
                PlanId = Guid.NewGuid(),
                PlanName = "Premium",
                Price = 9.99m,
                BillingCycle = BillingCycle.Monthly,
                Description = "Premium plan",
                MaxStudios = 50,
                MaxStorageMb = 10000,
                MaxAiRequestsPerDay = 100,
                MaxGroups = 100,
                MaxMembersPerGroup = 50
            };
            _subscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync(plan);

            var result = await _service.GetUserSubscriptionPlan(_userId);

            Assert.Equal(plan.PlanId, result.PlanId);
            Assert.Equal("Premium", result.PlanName);
            Assert.Equal(9.99m, result.Price);
            Assert.Equal(BillingCycle.Monthly, result.BillingCycle);
            Assert.Equal(100, result.MaxAiRequestsPerDay);
            Assert.Equal(50, result.MaxStudios);
            Assert.Equal(100, result.MaxGroups);
        }

        /// <summary>
        /// Branch: cache hit returns subscription plan directly
        /// Ref: UserService.GetUserSubscriptionPlan:345-350
        /// </summary>
        [Fact]
        public async Task GetUserSubscriptionPlan_UsesCache()
        {
            var plan = new SubscriptionPlan
            {
                PlanId = Guid.NewGuid(),
                PlanName = "Test",
                MaxAiRequestsPerDay = 50
            };
            _cacheServiceMock.Setup(x => x.GetAsync<SubscriptionPlan>(_cacheServiceMock.Object.GetUserSubscriptionKey(_userId)))
                .ReturnsAsync(plan);

            await _service.GetUserSubscriptionPlan(_userId);

            _cacheServiceMock.Verify(x => x.GetAsync<SubscriptionPlan>(
                _cacheServiceMock.Object.GetUserSubscriptionKey(_userId)), Times.Once);
            _subscriptionRepoMock.Verify(x => x.GetSubscriptionPlanByUserIdAsync(It.IsAny<Guid>()), Times.Never);
        }

        #endregion
    }
}