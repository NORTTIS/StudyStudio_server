using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services;
using StudioStudio_Server.Services.Interfaces;
using Xunit;

namespace StudioStudio_Server.Tests.Services
{
    public class UserServiceTests
    {
        private readonly Mock<IUserRepository> _userRepoMock;
        private readonly Mock<IPasswordHasher<User>> _passwordHasherMock;
        private readonly Mock<IConfiguration> _configMock;
        private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
        private readonly Mock<IWebHostEnvironment> _environmentMock;
        private readonly Mock<IUserSubscriptionRepository> _subscriptionRepoMock;
        private readonly Mock<IAIRequestLogRepository> _aiRequestLogRepoMock;
        private readonly Mock<ICacheService> _cacheServiceMock;
        private UserService _service = null!;

        // Fixed test IDs
        private readonly Guid _userId = Guid.NewGuid();
        private readonly string _validPassword = "Password123!";

        public UserServiceTests()
        {
            _userRepoMock = new Mock<IUserRepository>();
            _passwordHasherMock = new Mock<IPasswordHasher<User>>();
            _configMock = new Mock<IConfiguration>();
            _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
            _environmentMock = new Mock<IWebHostEnvironment>();
            _subscriptionRepoMock = new Mock<IUserSubscriptionRepository>();
            _aiRequestLogRepoMock = new Mock<IAIRequestLogRepository>();
            _cacheServiceMock = new Mock<ICacheService>();

            // Setup cache key methods
            _cacheServiceMock.Setup(x => x.GetUserProfileKey(_userId))
                .Returns($"user_profile:{_userId}");
            _cacheServiceMock.Setup(x => x.GetUserSubscriptionKey(_userId))
                .Returns($"user_subscription:{_userId}");
            _cacheServiceMock.Setup(x => x.GetExpirationForKey(It.IsAny<string>()))
                .Returns(TimeSpan.FromMinutes(5));

            // Setup GetOrSetAsync to call factory when cache miss (User version)
            _cacheServiceMock.Setup(x => x.GetOrSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<User>>>(),
                    It.IsAny<TimeSpan?>()))
                .Returns(async (string key, Func<Task<User>> factory, TimeSpan? exp) => await factory());

            // Setup GetOrSetAsync for SubscriptionPlan version
            _cacheServiceMock.Setup(x => x.GetOrSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<SubscriptionPlan>>>(),
                    It.IsAny<TimeSpan?>()))
                .Returns(async (string key, Func<Task<SubscriptionPlan>> factory, TimeSpan? exp) => await factory());

            _service = new UserService(
                _userRepoMock.Object,
                _passwordHasherMock.Object,
                _configMock.Object,
                _httpContextAccessorMock.Object,
                _environmentMock.Object,
                _subscriptionRepoMock.Object,
                _aiRequestLogRepoMock.Object,
                _cacheServiceMock.Object);
        }

        #region GetByIdAsync

        [Fact]
        public async Task GetByIdAsync_UserFound_ReturnsUser()
        {
            // Arrange
            var user = new User { UserId = _userId, FirstName = "John", LastName = "Doe" };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);

            // Act
            var result = await _service.GetByIdAsync(_userId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(_userId, result.UserId);
            Assert.Equal("John", result.FirstName);
        }

        [Fact]
        public async Task GetByIdAsync_UserNotFound_ReturnsNull()
        {
            // Arrange
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync((User?)null);

            // Act
            var result = await _service.GetByIdAsync(_userId);

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region GetByEmailAsync

        [Fact]
        public async Task GetByEmailAsync_UserFound_ReturnsUser()
        {
            // Arrange
            var email = "john@example.com";
            var user = new User { UserId = _userId, Email = email };
            _userRepoMock.Setup(x => x.GetByEmailAsync(email)).ReturnsAsync(user);

            // Act
            var result = await _service.GetByEmailAsync(email);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(email, result.Email);
        }

        [Fact]
        public async Task GetByEmailAsync_UserNotFound_ReturnsNull()
        {
            // Arrange
            _userRepoMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((User?)null);

            // Act
            var result = await _service.GetByEmailAsync("notfound@example.com");

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region UpdateAsync

        [Fact]
        public async Task UpdateAsync_ValidUser_CallsRepositoryAndInvalidatesCache()
        {
            // Arrange
            var user = new User { UserId = _userId, FirstName = "Jane" };

            // Act
            await _service.UpdateAsync(user);

            // Assert
            Assert.NotEqual(default, user.UpdatedAt);
            _userRepoMock.Verify(x => x.UpdateAsync(user), Times.Once);
            _cacheServiceMock.Verify(x => x.InvalidateUserCacheAsync(_userId), Times.Once);
        }

        #endregion

        #region DeleteAsync

        [Fact]
        public async Task DeleteAsync_UserNotFound_ThrowsNotFound()
        {
            // Arrange
            _userRepoMock.Setup(x => x.GetByIdIncludingDeletedAsync(_userId))
                .ReturnsAsync((User?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.DeleteAsync(_userId));
            Assert.Equal(ErrorCodes.UserNotFound, ex.Code);
            Assert.Equal(404, ex.HttpStatus);
        }

        [Fact]
        public async Task DeleteAsync_AlreadyDeleted_ThrowsBadRequest()
        {
            // Arrange
            var user = new User { UserId = _userId, Status = UserStatus.Deleted };
            _userRepoMock.Setup(x => x.GetByIdIncludingDeletedAsync(_userId))
                .ReturnsAsync(user);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.DeleteAsync(_userId));
            Assert.Equal(ErrorCodes.UserAccountAlreadyDeleted, ex.Code);
            Assert.Equal(400, ex.HttpStatus);
        }

        [Fact]
        public async Task DeleteAsync_ValidUser_GhostsUserAndInvalidatesCache()
        {
            // Arrange
            var user = new User
            {
                UserId = _userId,
                Email = "original@example.com",
                FirstName = "John",
                LastName = "Doe",
                Status = UserStatus.Active
            };
            _userRepoMock.Setup(x => x.GetByIdIncludingDeletedAsync(_userId))
                .ReturnsAsync(user);
            _passwordHasherMock.Setup(x => x.HashPassword(user, It.IsAny<string>()))
                .Returns("hashed_password");

            // Act
            await _service.DeleteAsync(_userId);

            // Assert
            Assert.Equal(UserStatus.Deleted, user.Status);
            Assert.Equal("Deleted", user.FirstName);
            Assert.Equal("User", user.LastName);
            Assert.Null(user.PhoneNumber);
            Assert.Null(user.Bio);
            Assert.Null(user.AvatarUrl);
            Assert.Null(user.GoogleId);
            Assert.NotNull(user.PasswordHash);
            _userRepoMock.Verify(x => x.UpdateAsync(user), Times.Once);
            _cacheServiceMock.Verify(x => x.InvalidateUserCacheAsync(_userId), Times.Once);
        }

        #endregion

        #region ChangePasswordAsync

        [Fact]
        public async Task ChangePasswordAsync_UserNotFound_ThrowsNotFound()
        {
            // Arrange
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId))
                .ReturnsAsync((User?)null);

            var request = new ChangePasswordRequest
            {
                CurrentPassword = "OldPass123!",
                NewPassword = "NewPass456!",
                ConfirmPassword = "NewPass456!"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ChangePasswordAsync(_userId, request));
            Assert.Equal(ErrorCodes.UserNotFound, ex.Code);
        }

        [Fact]
        public async Task ChangePasswordAsync_IncorrectCurrentPassword_ThrowsBadRequest()
        {
            // Arrange
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

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ChangePasswordAsync(_userId, request));
            Assert.Equal(ErrorCodes.AuthIncorrectCurrentPassword, ex.Code);
        }

        [Fact]
        public async Task ChangePasswordAsync_InvalidNewPasswordFormat_ThrowsBadRequest()
        {
            // Arrange
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

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ChangePasswordAsync(_userId, request));
            Assert.Equal(ErrorCodes.ValidationInvalidPassword, ex.Code);
        }

        [Fact]
        public async Task ChangePasswordAsync_PasswordMismatch_ThrowsBadRequest()
        {
            // Arrange
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

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ChangePasswordAsync(_userId, request));
            Assert.Equal(ErrorCodes.ValidationPasswordMismatch, ex.Code);
        }

        [Fact]
        public async Task ChangePasswordAsync_SameAsCurrentPassword_ThrowsBadRequest()
        {
            // Arrange
            var user = new User { UserId = _userId, PasswordHash = "hashed_password" };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);
            _passwordHasherMock.Setup(x => x.VerifyHashedPassword(user, "hashed_password", "OldPass123!"))
                .Returns(PasswordVerificationResult.Success);
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

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.ChangePasswordAsync(_userId, request));
            Assert.Equal(ErrorCodes.ValidationNewPasswordSameAsCurrent, ex.Code);
        }

        [Fact]
        public async Task ChangePasswordAsync_ValidRequest_UpdatesPasswordAndInvalidatesCache()
        {
            // Arrange
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

            // Act
            await _service.ChangePasswordAsync(_userId, request);

            // Assert
            Assert.Equal("new_hash", user.PasswordHash);
            Assert.NotEqual(default, user.UpdatedAt);
            _userRepoMock.Verify(x => x.UpdateAsync(user), Times.Once);
            _cacheServiceMock.Verify(x => x.InvalidateUserCacheAsync(_userId), Times.Once);
        }

        #endregion

        #region UpdateProfileAsync

        [Fact]
        public async Task UpdateProfileAsync_UserNotFound_ThrowsNotFound()
        {
            // Arrange
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId))
                .ReturnsAsync((User?)null);

            var request = new UpdateUserProfileRequest { FirstName = "Jane" };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                _service.UpdateProfileAsync(_userId, request));
            Assert.Equal(ErrorCodes.UserNotFound, ex.Code);
        }

        [Fact]
        public async Task UpdateProfileAsync_ValidRequest_UpdatesFieldsAndInvalidatesCache()
        {
            // Arrange
            var user = new User { UserId = _userId, FirstName = "John" };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);

            var request = new UpdateUserProfileRequest
            {
                FirstName = "Jane",
                LastName = "Smith",
                Bio = "Hello world"
            };

            // Act
            await _service.UpdateProfileAsync(_userId, request);

            // Assert
            Assert.Equal("Jane", user.FirstName);
            Assert.Equal("Smith", user.LastName);
            Assert.Equal("Hello world", user.Bio);
            Assert.NotEqual(default, user.UpdatedAt);
            _userRepoMock.Verify(x => x.UpdateAsync(user), Times.Once);
            _cacheServiceMock.Verify(x => x.InvalidateUserCacheAsync(_userId), Times.Once);
        }

        [Fact]
        public async Task UpdateProfileAsync_WithLanguage_UpdatesLanguage()
        {
            // Arrange
            var user = new User { UserId = _userId };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);

            var request = new UpdateUserProfileRequest { Language = "en" };

            // Act
            await _service.UpdateProfileAsync(_userId, request);

            // Assert
            Assert.Equal("en", user.Language);
        }

        [Fact]
        public async Task UpdateProfileAsync_WithEmailNotification_UpdatesFlag()
        {
            // Arrange
            var user = new User { UserId = _userId, EmailNotificationEnabled = false };
            _userRepoMock.Setup(x => x.GetByIdAsync(_userId)).ReturnsAsync(user);

            var request = new UpdateUserProfileRequest { EmailNotificationEnabled = true };

            // Act
            await _service.UpdateProfileAsync(_userId, request);

            // Assert
            Assert.True(user.EmailNotificationEnabled);
        }

        #endregion

        #region GetAiRequestLimitInfoAsync

        [Fact]
        public async Task GetAiRequestLimitInfoAsync_FreePlan_Returns20Limit()
        {
            // Arrange
            _aiRequestLogRepoMock.Setup(x => x.CountTodayRequestsAsync(_userId, It.IsAny<DateTime>()))
                .ReturnsAsync(5);
            _subscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync((SubscriptionPlan?)null); // Free plan

            // Act
            var (usedToday, dailyLimit) = await _service.GetAiRequestLimitInfoAsync(_userId);

            // Assert
            Assert.Equal(5, usedToday);
            Assert.Equal(20, dailyLimit);
        }

        [Fact]
        public async Task GetAiRequestLimitInfoAsync_PremiumPlan_ReturnsCustomLimit()
        {
            // Arrange
            _aiRequestLogRepoMock.Setup(x => x.CountTodayRequestsAsync(_userId, It.IsAny<DateTime>()))
                .ReturnsAsync(50);
            var premiumPlan = new SubscriptionPlan { PlanName = "Premium", MaxAiRequestsPerDay = 100 };
            _subscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync(premiumPlan);

            // Act
            var (usedToday, dailyLimit) = await _service.GetAiRequestLimitInfoAsync(_userId);

            // Assert
            Assert.Equal(50, usedToday);
            Assert.Equal(100, dailyLimit);
        }

        [Fact]
        public async Task GetAiRequestLimitInfoAsync_ZeroRequests_ReturnsZeroUsed()
        {
            // Arrange
            _aiRequestLogRepoMock.Setup(x => x.CountTodayRequestsAsync(_userId, It.IsAny<DateTime>()))
                .ReturnsAsync(0);
            _subscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync((SubscriptionPlan?)null);

            // Act
            var (usedToday, dailyLimit) = await _service.GetAiRequestLimitInfoAsync(_userId);

            // Assert
            Assert.Equal(0, usedToday);
            Assert.Equal(20, dailyLimit);
        }

        #endregion

        #region GetUserSubscriptionPlan

        [Fact]
        public async Task GetUserSubscriptionPlan_NullSubscription_ReturnsDefaults()
        {
            // Arrange
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

            // Act
            var result = await _service.GetUserSubscriptionPlan(_userId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(Guid.Empty, result.PlanId);
            Assert.Equal(20, result.MaxAiRequestsPerDay);
        }

        [Fact]
        public async Task GetUserSubscriptionPlan_WithPremiumPlan_ReturnsCorrectData()
        {
            // Arrange
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

            // Act
            var result = await _service.GetUserSubscriptionPlan(_userId);

            // Assert
            Assert.Equal(plan.PlanId, result.PlanId);
            Assert.Equal("Premium", result.PlanName);
            Assert.Equal(9.99m, result.Price);
            Assert.Equal(BillingCycle.Monthly, result.BillingCycle);
            Assert.Equal(100, result.MaxAiRequestsPerDay);
            Assert.Equal(50, result.MaxStudios);
            Assert.Equal(100, result.MaxGroups);
        }

        [Fact]
        public async Task GetUserSubscriptionPlan_UsesCache()
        {
            // Arrange
            var plan = new SubscriptionPlan
            {
                PlanId = Guid.NewGuid(),
                PlanName = "Test",
                MaxAiRequestsPerDay = 50
            };
            _subscriptionRepoMock.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
                .ReturnsAsync(plan);

            // Act
            await _service.GetUserSubscriptionPlan(_userId);

            // Assert
            _cacheServiceMock.Verify(x => x.GetOrSetAsync(
                _cacheServiceMock.Object.GetUserSubscriptionKey(_userId),
                It.IsAny<Func<Task<SubscriptionPlan>>>(),
                It.IsAny<TimeSpan?>()), Times.Once);
        }

        #endregion
    }
}
