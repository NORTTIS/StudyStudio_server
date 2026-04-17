using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Moq;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services;
using Xunit;

namespace StudioStudio_Server.Tests.Services;

/// <summary>
/// Unit tests cho AuthService.
/// Tests: register, verify email, login, refresh token, Google OAuth, password reset.
/// Ref: Services/AuthService.cs
/// </summary>
public class AuthServiceTests
{
    private static readonly Guid TestUserId = Guid.NewGuid();
    private static readonly string TestEmail = "test@example.com";
    private static readonly string TestPassword = "Password123!";

    // =====================================================================
    // REGISTERASYNC TESTS - Đăng ký tài khoản
    // =====================================================================

    [Fact]
    public async Task RegisterAsync_InvalidEmailFormat_ThrowsAppException()
    {
        var (service, _) = CreateAuthService(out var mocks);
        var request = new RegisterRequests
        {
            Email = "invalid-email",
            Password = TestPassword,
            FirstName = "Test",
            LastName = "User"
        };

        var ex = await Assert.ThrowsAsync<AppException>(() => service.RegisterAsync(request));
        Assert.Equal(ErrorCodes.ValidationInvalidEmail, ex.Code);
    }

    [Fact]
    public async Task RegisterAsync_InvalidPasswordFormat_ThrowsAppException()
    {
        var (service, _) = CreateAuthService(out var mocks);
        var request = new RegisterRequests
        {
            Email = TestEmail,
            Password = "weak",
            FirstName = "Test",
            LastName = "User"
        };

        var ex = await Assert.ThrowsAsync<AppException>(() => service.RegisterAsync(request));
        Assert.Equal(ErrorCodes.ValidationInvalidPassword, ex.Code);
    }

    [Fact]
    public async Task RegisterAsync_RateLimitExceeded_ThrowsAppException()
    {
        var (service, mocks) = CreateAuthService(out _);
        mocks.EmailVerificationCache
            .Setup(x => x.CanSendVerificationEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        var request = new RegisterRequests
        {
            Email = TestEmail,
            Password = TestPassword,
            FirstName = "Test",
            LastName = "User"
        };

        var ex = await Assert.ThrowsAsync<AppException>(() => service.RegisterAsync(request));
        Assert.Equal(ErrorCodes.ValidationRateLimitExceeded, ex.Code);
        Assert.Equal(StatusCodes.Status429TooManyRequests, ex.HttpStatus);
    }

    [Fact]
    public async Task RegisterAsync_EmailAlreadyExists_ThrowsAppException()
    {
        var (service, mocks) = CreateAuthService(out _);
        mocks.EmailVerificationCache
            .Setup(x => x.CanSendVerificationEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(true);
        mocks.UserRepository
            .Setup(x => x.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new User());

        var request = new RegisterRequests
        {
            Email = TestEmail,
            Password = TestPassword,
            FirstName = "Test",
            LastName = "User"
        };

        var ex = await Assert.ThrowsAsync<AppException>(() => service.RegisterAsync(request));
        Assert.Equal(ErrorCodes.UserAlreadyExist, ex.Code);
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_CreatesUserAndSendsEmail()
    {
        var (service, mocks) = CreateAuthService(out _);
        mocks.EmailVerificationCache
            .Setup(x => x.CanSendVerificationEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(true);
        mocks.UserRepository
            .Setup(x => x.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);
        mocks.UserRepository
            .Setup(x => x.AddAsync(It.IsAny<User>()))
            .Returns(Task.CompletedTask);
        mocks.EmailVerificationCache
            .Setup(x => x.StoreVerificationTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<TimeSpan>()))
            .Returns(Task.CompletedTask);
        mocks.EmailVerificationCache
            .Setup(x => x.IncrementSendCountAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        mocks.EmailService
            .Setup(x => x.SendLinkAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var request = new RegisterRequests
        {
            Email = TestEmail,
            Password = TestPassword,
            FirstName = "Test",
            LastName = "User"
        };

        await service.RegisterAsync(request);

        mocks.UserRepository.Verify(x => x.AddAsync(It.Is<User>(u =>
            u.Email == TestEmail &&
            u.FirstName == "Test" &&
            u.LastName == "User" &&
            u.Status == UserStatus.Active &&
            u.IsVerify == false)), Times.Once);

        mocks.EmailService.Verify(x => x.SendLinkAsync(
            TestEmail, "Xác thực tài khoản của bạn", It.IsAny<string>()), Times.Once);
    }

    // =====================================================================
    // VERIFYEMAILLINKASYNC TESTS - Xác thực email
    // =====================================================================

    [Fact]
    public async Task VerifyEmailLinkAsync_InvalidToken_ThrowsAppException()
    {
        var (service, mocks) = CreateAuthService(out _);
        mocks.EmailVerificationCache
            .Setup(x => x.GetVerificationDataByTokenAsync(It.IsAny<string>()))
            .ReturnsAsync((EmailVerificationDataRedis?)null);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.VerifyEmailLinkAsync("invalid-token"));
        Assert.Equal(ErrorCodes.ValidationInvalidToken, ex.Code);
    }

    [Fact]
    public async Task VerifyEmailLinkAsync_UserNotFound_ThrowsAppException()
    {
        var (service, mocks) = CreateAuthService(out _);
        mocks.EmailVerificationCache
            .Setup(x => x.GetVerificationDataByTokenAsync(It.IsAny<string>()))
            .ReturnsAsync(new EmailVerificationDataRedis { Email = TestEmail, UserId = TestUserId });
        mocks.UserRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((User?)null);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.VerifyEmailLinkAsync("valid-token"));
        Assert.Equal(ErrorCodes.UserNotFound, ex.Code);
    }

    [Fact]
    public async Task VerifyEmailLinkAsync_UserDeleted_ThrowsAppException()
    {
        var (service, mocks) = CreateAuthService(out _);
        mocks.EmailVerificationCache
            .Setup(x => x.GetVerificationDataByTokenAsync(It.IsAny<string>()))
            .ReturnsAsync(new EmailVerificationDataRedis { Email = TestEmail, UserId = TestUserId });
        mocks.UserRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new User { Status = UserStatus.Deleted });

        var ex = await Assert.ThrowsAsync<AppException>(() => service.VerifyEmailLinkAsync("valid-token"));
        Assert.Equal(ErrorCodes.UserAccountAlreadyDeleted, ex.Code);
    }

    [Fact]
    public async Task VerifyEmailLinkAsync_UserInactive_ThrowsAppException()
    {
        var (service, mocks) = CreateAuthService(out _);
        mocks.EmailVerificationCache
            .Setup(x => x.GetVerificationDataByTokenAsync(It.IsAny<string>()))
            .ReturnsAsync(new EmailVerificationDataRedis { Email = TestEmail, UserId = TestUserId });
        mocks.UserRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new User { Status = UserStatus.Inactive });

        var ex = await Assert.ThrowsAsync<AppException>(() => service.VerifyEmailLinkAsync("valid-token"));
        Assert.Equal(ErrorCodes.AuthAccountInactive, ex.Code);
    }

    [Fact]
    public async Task VerifyEmailLinkAsync_EmailAlreadyVerified_ThrowsAppException()
    {
        var (service, mocks) = CreateAuthService(out _);
        mocks.EmailVerificationCache
            .Setup(x => x.GetVerificationDataByTokenAsync(It.IsAny<string>()))
            .ReturnsAsync(new EmailVerificationDataRedis { Email = TestEmail, UserId = TestUserId });
        mocks.UserRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new User { Status = UserStatus.Active, IsVerify = true });

        var ex = await Assert.ThrowsAsync<AppException>(() => service.VerifyEmailLinkAsync("valid-token"));
        Assert.Equal(ErrorCodes.ValidationEmailAlreadyVerified, ex.Code);
    }

    [Fact]
    public async Task VerifyEmailLinkAsync_ValidToken_UpdatesUserAndInvalidatesToken()
    {
        var (service, mocks) = CreateAuthService(out _);
        mocks.EmailVerificationCache
            .Setup(x => x.GetVerificationDataByTokenAsync(It.IsAny<string>()))
            .ReturnsAsync(new EmailVerificationDataRedis { Email = TestEmail, UserId = TestUserId });

        var user = new User
        {
            UserId = TestUserId,
            Email = TestEmail,
            Status = UserStatus.Active,
            IsVerify = false
        };
        mocks.UserRepository
            .Setup(x => x.GetByIdAsync(TestUserId))
            .ReturnsAsync(user);
        mocks.UserRepository
            .Setup(x => x.UpdateAsync(It.IsAny<User>()))
            .Returns(Task.CompletedTask);
        mocks.EmailVerificationCache
            .Setup(x => x.InvalidateVerificationTokenAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        await service.VerifyEmailLinkAsync("valid-token");

        Assert.True(user.IsVerify);
        mocks.UserRepository.Verify(x => x.UpdateAsync(It.Is<User>(u => u.IsVerify == true)), Times.Once);
        mocks.EmailVerificationCache.Verify(x => x.InvalidateVerificationTokenAsync(TestEmail), Times.Once);
    }

    // =====================================================================
    // LOGINASYNC TESTS - Đăng nhập
    // =====================================================================

    [Fact]
    public async Task LoginAsync_InvalidEmailFormat_ThrowsAppException()
    {
        var (service, _) = CreateAuthService(out var mocks);
        var request = new LoginRequests { Email = "invalid", Password = TestPassword };

        var ex = await Assert.ThrowsAsync<AppException>(() => service.LoginAsync(request, It.IsAny<HttpResponse>()));
        Assert.Equal(ErrorCodes.ValidationInvalidEmail, ex.Code);
    }

    [Fact]
    public async Task LoginAsync_UserNotFound_ThrowsAppException()
    {
        var (service, mocks) = CreateAuthService(out _);
        var request = new LoginRequests { Email = TestEmail, Password = TestPassword };
        mocks.UserRepository
            .Setup(x => x.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.LoginAsync(request, It.IsAny<HttpResponse>()));
        Assert.Equal(ErrorCodes.AuthInvalidCredential, ex.Code);
    }

    [Fact]
    public async Task LoginAsync_UserDeleted_ThrowsAppException()
    {
        var (service, mocks) = CreateAuthService(out _);
        var request = new LoginRequests { Email = TestEmail, Password = TestPassword };
        mocks.UserRepository
            .Setup(x => x.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new User { Status = UserStatus.Deleted });

        var ex = await Assert.ThrowsAsync<AppException>(() => service.LoginAsync(request, It.IsAny<HttpResponse>()));
        Assert.Equal(ErrorCodes.UserAccountAlreadyDeleted, ex.Code);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsAppException()
    {
        var (service, mocks) = CreateAuthService(out _);
        var request = new LoginRequests { Email = TestEmail, Password = "WrongPassword123!" };

        var user = new User
        {
            UserId = TestUserId,
            Email = TestEmail,
            Status = UserStatus.Active,
            IsVerify = true,
            PasswordHash = "hashed"
        };
        mocks.UserRepository
            .Setup(x => x.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(user);
        mocks.PasswordHasher
            .Setup(x => x.VerifyHashedPassword(user, user.PasswordHash!, It.IsAny<string>()))
            .Returns(PasswordVerificationResult.Failed);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.LoginAsync(request, It.IsAny<HttpResponse>()));
        Assert.Equal(ErrorCodes.AuthInvalidCredential, ex.Code);
    }

    [Fact]
    public async Task LoginAsync_UserInactive_ThrowsAppException()
    {
        var (service, mocks) = CreateAuthService(out _);
        var request = new LoginRequests { Email = TestEmail, Password = TestPassword };

        var user = new User
        {
            UserId = TestUserId,
            Email = TestEmail,
            Status = UserStatus.Inactive,
            IsVerify = false,
            PasswordHash = "hashed"
        };
        mocks.UserRepository
            .Setup(x => x.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(user);
        mocks.PasswordHasher
            .Setup(x => x.VerifyHashedPassword(user, user.PasswordHash!, It.IsAny<string>()))
            .Returns(PasswordVerificationResult.Success);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.LoginAsync(request, It.IsAny<HttpResponse>()));
        Assert.Equal(ErrorCodes.AuthAccountInactive, ex.Code);
    }

    [Fact]
    public async Task LoginAsync_EmailNotVerified_ThrowsAppException()
    {
        var (service, mocks) = CreateAuthService(out _);
        var request = new LoginRequests { Email = TestEmail, Password = TestPassword };

        var user = new User
        {
            UserId = TestUserId,
            Email = TestEmail,
            Status = UserStatus.Active,
            IsVerify = false,
            PasswordHash = "hashed"
        };
        mocks.UserRepository
            .Setup(x => x.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(user);
        mocks.PasswordHasher
            .Setup(x => x.VerifyHashedPassword(user, user.PasswordHash!, It.IsAny<string>()))
            .Returns(PasswordVerificationResult.Success);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.LoginAsync(request, It.IsAny<HttpResponse>()));
        Assert.Equal(ErrorCodes.AuthAccountNotVerified, ex.Code);
    }

    [Fact(Skip = "Cannot mock IConfiguration.GetValue<T> extension method - requires refactoring AuthService to use IOptions<JwtSettings>")]
    public async Task LoginAsync_ValidCredentials_ReturnsLoginResponse()
    {
        var (service, mocks) = CreateAuthService(out _);
        var request = new LoginRequests { Email = TestEmail, Password = TestPassword };
        var httpResponse = CreateMockHttpResponse();

        var user = new User
        {
            UserId = TestUserId,
            Email = TestEmail,
            FirstName = "Test",
            LastName = "User",
            Status = UserStatus.Active,
            IsVerify = true,
            IsAdmin = false,
            PasswordHash = "hashed"
        };
        mocks.UserRepository
            .Setup(x => x.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(user);
        mocks.PasswordHasher
            .Setup(x => x.VerifyHashedPassword(user, user.PasswordHash!, It.IsAny<string>()))
            .Returns(PasswordVerificationResult.Success);
        mocks.RefreshTokenRepository
            .Setup(x => x.AddAsync(It.IsAny<RefreshToken>()))
            .Returns(Task.CompletedTask);
        mocks.Configuration
            .Setup(x => x["JWT:AccessTokenExpireMs"])
            .Returns("3600000");
        mocks.Configuration
            .Setup(x => x["JWT:RefreshTokenExpireMs"])
            .Returns("86400000");
        mocks.Configuration
            .Setup(x => x["JWT:Key"])
            .Returns("super-secret-key-that-is-at-least-32-characters-long!!");
        mocks.Configuration
            .Setup(x => x["JWT:Issuer"])
            .Returns("StudioStudio");
        mocks.Configuration
            .Setup(x => x["JWT:Audience"])
            .Returns("StudioStudioClient");

        var result = await service.LoginAsync(request, httpResponse);

        Assert.NotNull(result);
        Assert.Equal(TestUserId, result.Id);
        Assert.Equal(TestEmail, result.Email);
        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
        mocks.RefreshTokenRepository.Verify(x => x.AddAsync(It.IsAny<RefreshToken>()), Times.Once);
    }

    // =====================================================================
    // REFRESHTOKENASYNC TESTS - Làm mới token
    // =====================================================================

    [Fact]
    public async Task RefreshTokenAsync_InvalidToken_ThrowsAppException()
    {
        var (service, mocks) = CreateAuthService(out _);
        mocks.RefreshTokenRepository
            .Setup(x => x.GetValidAsync(It.IsAny<string>()))
            .ReturnsAsync((RefreshToken?)null);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.RefreshTokenAsync("invalid-token", It.IsAny<HttpResponse>()));
        Assert.Equal(ErrorCodes.AuthTokenExpired, ex.Code);
    }

    [Fact]
    public async Task RefreshTokenAsync_RevokedToken_ThrowsAppException()
    {
        var (service, mocks) = CreateAuthService(out _);
        mocks.RefreshTokenRepository
            .Setup(x => x.GetValidAsync(It.IsAny<string>()))
            .ReturnsAsync(new RefreshToken
            {
                Id = Guid.NewGuid(),
                Token = "token",
                UserId = TestUserId,
                IsRevoked = true,
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            });

        var ex = await Assert.ThrowsAsync<AppException>(() => service.RefreshTokenAsync("revoked-token", It.IsAny<HttpResponse>()));
        Assert.Equal(ErrorCodes.AuthTokenExpired, ex.Code);
    }

    [Fact]
    public async Task RefreshTokenAsync_ExpiredToken_ThrowsAppException()
    {
        var (service, mocks) = CreateAuthService(out _);
        mocks.RefreshTokenRepository
            .Setup(x => x.GetValidAsync(It.IsAny<string>()))
            .ReturnsAsync(new RefreshToken
            {
                Id = Guid.NewGuid(),
                Token = "token",
                UserId = TestUserId,
                IsRevoked = false,
                ExpiresAt = DateTime.UtcNow.AddDays(-1)
            });

        var ex = await Assert.ThrowsAsync<AppException>(() => service.RefreshTokenAsync("expired-token", It.IsAny<HttpResponse>()));
        Assert.Equal(ErrorCodes.AuthTokenExpired, ex.Code);
    }

    [Fact]
    public async Task RefreshTokenAsync_UserNotFound_ThrowsAppException()
    {
        var (service, mocks) = CreateAuthService(out _);
        mocks.RefreshTokenRepository
            .Setup(x => x.GetValidAsync(It.IsAny<string>()))
            .ReturnsAsync(new RefreshToken
            {
                Id = Guid.NewGuid(),
                Token = "valid-token",
                UserId = TestUserId,
                IsRevoked = false,
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            });
        mocks.RefreshTokenRepository.Setup(x => x.RevokeAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);
        mocks.RefreshTokenRepository.Setup(x => x.CleanupUserTokensAsync(It.IsAny<Guid>())).ReturnsAsync(0);
        mocks.UserRepository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.RefreshTokenAsync("valid-token", It.IsAny<HttpResponse>()));
        Assert.Equal(ErrorCodes.UserNotFound, ex.Code);
    }

    [Fact]
    public async Task RefreshTokenAsync_UserDeleted_ThrowsAppException()
    {
        var (service, mocks) = CreateAuthService(out _);
        mocks.RefreshTokenRepository
            .Setup(x => x.GetValidAsync(It.IsAny<string>()))
            .ReturnsAsync(new RefreshToken
            {
                Id = Guid.NewGuid(),
                Token = "valid-token",
                UserId = TestUserId,
                IsRevoked = false,
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            });
        mocks.RefreshTokenRepository.Setup(x => x.RevokeAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);
        mocks.RefreshTokenRepository.Setup(x => x.CleanupUserTokensAsync(It.IsAny<Guid>())).ReturnsAsync(0);
        mocks.UserRepository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new User { Status = UserStatus.Deleted });

        var ex = await Assert.ThrowsAsync<AppException>(() => service.RefreshTokenAsync("valid-token", It.IsAny<HttpResponse>()));
        Assert.Equal(ErrorCodes.UserAccountAlreadyDeleted, ex.Code);
    }

    [Fact(Skip = "Cannot mock IConfiguration.GetValue<T> extension method - requires refactoring AuthService to use IOptions<JwtSettings>")]
    public async Task RefreshTokenAsync_ValidToken_ReturnsNewTokens()
    {
        var (service, mocks) = CreateAuthService(out _);
        var httpResponse = CreateMockHttpResponse();

        mocks.RefreshTokenRepository
            .Setup(x => x.GetValidAsync(It.IsAny<string>()))
            .ReturnsAsync(new RefreshToken
            {
                Id = Guid.NewGuid(),
                Token = "valid-token",
                UserId = TestUserId,
                IsRevoked = false,
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            });
        mocks.RefreshTokenRepository.Setup(x => x.RevokeAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);
        mocks.RefreshTokenRepository.Setup(x => x.CleanupUserTokensAsync(It.IsAny<Guid>())).ReturnsAsync(0);
        mocks.RefreshTokenRepository.Setup(x => x.AddAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);
        mocks.UserRepository.Setup(x => x.GetByIdAsync(TestUserId)).ReturnsAsync(new User
        {
            UserId = TestUserId,
            Email = TestEmail,
            FirstName = "Test",
            LastName = "User",
            Status = UserStatus.Active,
            IsAdmin = false
        });
        mocks.Configuration.Setup(x => x["JWT:AccessTokenExpireMs"]).Returns("3600000");
        mocks.Configuration.Setup(x => x["JWT:RefreshTokenExpireMs"]).Returns("86400000");
        mocks.Configuration.Setup(x => x["JWT:Key"]).Returns("super-secret-key-that-is-at-least-32-characters-long!!");
        mocks.Configuration.Setup(x => x["JWT:Issuer"]).Returns("StudioStudio");
        mocks.Configuration.Setup(x => x["JWT:Audience"]).Returns("StudioStudioClient");

        var result = await service.RefreshTokenAsync("valid-token", httpResponse);

        Assert.NotNull(result);
        Assert.Equal(TestUserId, result.Id);
        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
    }

    // =====================================================================
    // LOGOUTASYNC TESTS - Đăng xuất
    // =====================================================================

    [Fact]
    public async Task LogoutAsync_ValidToken_RevokesTokenAndDeletesCookie()
    {
        var (service, mocks) = CreateAuthService(out _);
        var httpResponse = CreateMockHttpResponse();

        mocks.RefreshTokenRepository
            .Setup(x => x.GetValidAsync(It.IsAny<string>()))
            .ReturnsAsync(new RefreshToken
            {
                Id = Guid.NewGuid(),
                Token = "valid-token",
                UserId = TestUserId,
                IsRevoked = false,
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            });
        mocks.RefreshTokenRepository.Setup(x => x.RevokeAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);
        mocks.RefreshTokenRepository.Setup(x => x.CleanupUserTokensAsync(It.IsAny<Guid>())).ReturnsAsync(0);

        await service.LogoutAsync("valid-token", httpResponse);

        mocks.RefreshTokenRepository.Verify(x => x.RevokeAsync(It.IsAny<RefreshToken>()), Times.Once);
    }

    [Fact]
    public async Task LogoutAsync_InvalidToken_DoesNothing()
    {
        var (service, mocks) = CreateAuthService(out _);
        var httpResponse = CreateMockHttpResponse();

        mocks.RefreshTokenRepository
            .Setup(x => x.GetValidAsync(It.IsAny<string>()))
            .ReturnsAsync((RefreshToken?)null);

        await service.LogoutAsync("invalid-token", httpResponse);

        mocks.RefreshTokenRepository.Verify(x => x.RevokeAsync(It.IsAny<RefreshToken>()), Times.Never);
    }

    // =====================================================================
    // GOOGLELOGINASYNC TESTS - Đăng nhập Google
    // =====================================================================

    [Fact]
    public async Task GoogleLoginAsync_InvalidGoogleToken_ThrowsAppException()
    {
        var (service, _) = CreateAuthService(out var mocks);
        var request = new GoogleLoginRequest { IdToken = "invalid-token" };

        var ex = await Assert.ThrowsAsync<AppException>(() => service.GoogleLoginAsync(request, It.IsAny<HttpResponse>()));
        Assert.Equal(ErrorCodes.AuthInvalidCredential, ex.Code);
    }

    [Fact(Skip = "Cannot mock GoogleJsonWebSignature.ValidateAsync (external Google API) - requires integration test setup")]
    public async Task GoogleLoginAsync_DeletedUser_ThrowsAppException()
    {
        var (service, mocks) = CreateAuthService(out _);
        var request = new GoogleLoginRequest { IdToken = "valid-google-token" };

        mocks.UserRepository
            .Setup(x => x.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(new User { Status = UserStatus.Deleted });

        var ex = await Assert.ThrowsAsync<AppException>(() => service.GoogleLoginAsync(request, It.IsAny<HttpResponse>()));
        Assert.Equal(ErrorCodes.UserAccountAlreadyDeleted, ex.Code);
    }

    // =====================================================================
    // SENDRESETPASSWORDLINKASYNC TESTS
    // =====================================================================

    [Fact]
    public async Task SendResetPasswordLinkAsync_InvalidEmailFormat_ThrowsAppException()
    {
        var (service, _) = CreateAuthService(out var mocks);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.SendResetPasswordLinkAsync("invalid"));
        Assert.Equal(ErrorCodes.ValidationInvalidEmail, ex.Code);
    }

    [Fact]
    public async Task SendResetPasswordLinkAsync_RateLimitExceeded_ThrowsAppException()
    {
        var (service, mocks) = CreateAuthService(out _);
        mocks.ResetCache.Setup(x => x.CanSendResetEmailAsync(It.IsAny<string>())).ReturnsAsync(false);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.SendResetPasswordLinkAsync(TestEmail));
        Assert.Equal(ErrorCodes.ValidationRateLimitExceeded, ex.Code);
    }

    [Fact]
    public async Task SendResetPasswordLinkAsync_UserNotFound_ThrowsAppException()
    {
        var (service, mocks) = CreateAuthService(out _);
        mocks.ResetCache.Setup(x => x.CanSendResetEmailAsync(It.IsAny<string>())).ReturnsAsync(true);
        mocks.UserRepository.Setup(x => x.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.SendResetPasswordLinkAsync(TestEmail));
        Assert.Equal(ErrorCodes.UserNotFound, ex.Code);
    }

    [Fact]
    public async Task SendResetPasswordLinkAsync_UserDeleted_ThrowsAppException()
    {
        var (service, mocks) = CreateAuthService(out _);
        mocks.ResetCache.Setup(x => x.CanSendResetEmailAsync(It.IsAny<string>())).ReturnsAsync(true);
        mocks.UserRepository.Setup(x => x.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync(new User { Status = UserStatus.Deleted });

        var ex = await Assert.ThrowsAsync<AppException>(() => service.SendResetPasswordLinkAsync(TestEmail));
        Assert.Equal(ErrorCodes.UserAccountAlreadyDeleted, ex.Code);
    }

    [Fact]
    public async Task SendResetPasswordLinkAsync_ValidEmail_SendsEmail()
    {
        var (service, mocks) = CreateAuthService(out _);
        var user = new User { UserId = TestUserId, Email = TestEmail, Status = UserStatus.Active };

        mocks.ResetCache.Setup(x => x.CanSendResetEmailAsync(It.IsAny<string>())).ReturnsAsync(true);
        mocks.UserRepository.Setup(x => x.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);
        mocks.ResetCache.Setup(x => x.StoreResetTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<TimeSpan>())).Returns(Task.CompletedTask);
        mocks.ResetCache.Setup(x => x.IncrementSendCountAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        mocks.EmailService.Setup(x => x.SendLinkAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        mocks.Configuration.Setup(x => x["Frontend:ResetPassURL"]).Returns("http://localhost:3000/reset");

        await service.SendResetPasswordLinkAsync(TestEmail);

        mocks.ResetCache.Verify(x => x.StoreResetTokenAsync(
            TestEmail, It.IsAny<string>(), TestUserId, TimeSpan.FromMinutes(15)), Times.Once);
        mocks.EmailService.Verify(x => x.SendLinkAsync(
            TestEmail, "Reset your password", It.IsAny<string>()), Times.Once);
    }

    // =====================================================================
    // VERIFYRESETTOKENASYNC TESTS - Xác thực token reset
    // =====================================================================

    [Fact]
    public async Task VerifyResetTokenAsync_InvalidToken_ReturnsFalse()
    {
        var (service, mocks) = CreateAuthService(out _);
        mocks.ResetCache.Setup(x => x.GetResetDataByTokenAsync(It.IsAny<string>())).ReturnsAsync((PasswordResetDataRedis?)null);

        var result = await service.VerifyResetTokenAsync("invalid-token");

        Assert.False(result);
    }

    [Fact]
    public async Task VerifyResetTokenAsync_UserNotFound_ReturnsFalse()
    {
        var (service, mocks) = CreateAuthService(out _);
        mocks.ResetCache
            .Setup(x => x.GetResetDataByTokenAsync(It.IsAny<string>()))
            .ReturnsAsync(new PasswordResetDataRedis { Email = TestEmail, UserId = TestUserId });
        mocks.UserRepository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);

        var result = await service.VerifyResetTokenAsync("valid-token");

        Assert.False(result);
    }

    [Fact]
    public async Task VerifyResetTokenAsync_UserDeleted_ReturnsFalse()
    {
        var (service, mocks) = CreateAuthService(out _);
        mocks.ResetCache
            .Setup(x => x.GetResetDataByTokenAsync(It.IsAny<string>()))
            .ReturnsAsync(new PasswordResetDataRedis { Email = TestEmail, UserId = TestUserId });
        mocks.UserRepository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new User { Status = UserStatus.Deleted });

        var result = await service.VerifyResetTokenAsync("valid-token");

        Assert.False(result);
    }

    [Fact]
    public async Task VerifyResetTokenAsync_ValidTokenAndUser_ReturnsTrue()
    {
        var (service, mocks) = CreateAuthService(out _);
        mocks.ResetCache
            .Setup(x => x.GetResetDataByTokenAsync(It.IsAny<string>()))
            .ReturnsAsync(new PasswordResetDataRedis { Email = TestEmail, UserId = TestUserId });
        mocks.UserRepository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new User { UserId = TestUserId, Status = UserStatus.Active });

        var result = await service.VerifyResetTokenAsync("valid-token");

        Assert.True(result);
    }

    // =====================================================================
    // RESETPASSWORDASYNC TESTS - Đặt lại mật khẩu
    // =====================================================================

    [Fact]
    public async Task ResetPasswordAsync_InvalidPassword_ThrowsAppException()
    {
        var (service, _) = CreateAuthService(out var mocks);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.ResetPasswordAsync("token", "weak"));
        Assert.Equal(ErrorCodes.ValidationInvalidPassword, ex.Code);
    }

    [Fact]
    public async Task ResetPasswordAsync_InvalidResetToken_ThrowsAppException()
    {
        var (service, mocks) = CreateAuthService(out _);
        mocks.ResetCache.Setup(x => x.GetResetDataByTokenAsync(It.IsAny<string>())).ReturnsAsync((PasswordResetDataRedis?)null);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.ResetPasswordAsync("invalid-token", TestPassword));
        Assert.Equal(ErrorCodes.ValidationInvalidToken, ex.Code);
    }

    [Fact]
    public async Task ResetPasswordAsync_UserNotFound_ThrowsAppException()
    {
        var (service, mocks) = CreateAuthService(out _);
        mocks.ResetCache
            .Setup(x => x.GetResetDataByTokenAsync(It.IsAny<string>()))
            .ReturnsAsync(new PasswordResetDataRedis { Email = TestEmail, UserId = TestUserId });
        mocks.UserRepository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.ResetPasswordAsync("token", TestPassword));
        Assert.Equal(ErrorCodes.UserNotFound, ex.Code);
    }

    [Fact]
    public async Task ResetPasswordAsync_UserDeleted_ThrowsAppException()
    {
        var (service, mocks) = CreateAuthService(out _);
        mocks.ResetCache
            .Setup(x => x.GetResetDataByTokenAsync(It.IsAny<string>()))
            .ReturnsAsync(new PasswordResetDataRedis { Email = TestEmail, UserId = TestUserId });
        mocks.UserRepository.Setup(x => x.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new User { Status = UserStatus.Deleted });

        var ex = await Assert.ThrowsAsync<AppException>(() => service.ResetPasswordAsync("token", TestPassword));
        Assert.Equal(ErrorCodes.UserAccountAlreadyDeleted, ex.Code);
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidRequest_UpdatesPasswordAndInvalidatesToken()
    {
        var (service, mocks) = CreateAuthService(out _);
        var user = new User
        {
            UserId = TestUserId,
            Email = TestEmail,
            Status = UserStatus.Active,
            PasswordHash = "old-hash"
        };

        mocks.ResetCache
            .Setup(x => x.GetResetDataByTokenAsync(It.IsAny<string>()))
            .ReturnsAsync(new PasswordResetDataRedis { Email = TestEmail, UserId = TestUserId });
        mocks.UserRepository.Setup(x => x.GetByIdAsync(TestUserId)).ReturnsAsync(user);
        mocks.UserRepository.Setup(x => x.UpdateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
        mocks.ResetCache.Setup(x => x.InvalidateResetTokenAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        mocks.RefreshTokenRepository.Setup(x => x.RevokeAllUserTokensAsync(It.IsAny<Guid>())).ReturnsAsync(5);

        await service.ResetPasswordAsync("valid-token", TestPassword);

        Assert.NotEqual("old-hash", user.PasswordHash);
        mocks.ResetCache.Verify(x => x.InvalidateResetTokenAsync(TestEmail), Times.Once);
    }

    // =====================================================================
    // RESENDVERIFYEMAILASYNC TESTS - Gửi lại email xác thực
    // =====================================================================

    [Fact]
    public async Task ResendVerifyEmailAsync_InvalidEmail_ThrowsAppException()
    {
        var (service, _) = CreateAuthService(out var mocks);
        var request = new ResendVerifyEmailRequest { Email = "invalid" };

        var ex = await Assert.ThrowsAsync<AppException>(() => service.ResendVerifyEmailAsync(request));
        Assert.Equal(ErrorCodes.ValidationInvalidEmail, ex.Code);
    }

    [Fact]
    public async Task ResendVerifyEmailAsync_RateLimitExceeded_ThrowsAppException()
    {
        var (service, mocks) = CreateAuthService(out _);
        var request = new ResendVerifyEmailRequest { Email = TestEmail };
        mocks.EmailVerificationCache.Setup(x => x.CanSendVerificationEmailAsync(It.IsAny<string>())).ReturnsAsync(false);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.ResendVerifyEmailAsync(request));
        Assert.Equal(ErrorCodes.ValidationRateLimitExceeded, ex.Code);
    }

    [Fact]
    public async Task ResendVerifyEmailAsync_UserNotFound_ThrowsAppException()
    {
        var (service, mocks) = CreateAuthService(out _);
        var request = new ResendVerifyEmailRequest { Email = TestEmail };
        mocks.EmailVerificationCache.Setup(x => x.CanSendVerificationEmailAsync(It.IsAny<string>())).ReturnsAsync(true);
        mocks.UserRepository.Setup(x => x.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((User?)null);

        var ex = await Assert.ThrowsAsync<AppException>(() => service.ResendVerifyEmailAsync(request));
        Assert.Equal(ErrorCodes.UserNotFound, ex.Code);
    }

    [Fact]
    public async Task ResendVerifyEmailAsync_UserDeleted_ThrowsAppException()
    {
        var (service, mocks) = CreateAuthService(out _);
        var request = new ResendVerifyEmailRequest { Email = TestEmail };
        mocks.EmailVerificationCache.Setup(x => x.CanSendVerificationEmailAsync(It.IsAny<string>())).ReturnsAsync(true);
        mocks.UserRepository.Setup(x => x.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync(new User { Status = UserStatus.Deleted });

        var ex = await Assert.ThrowsAsync<AppException>(() => service.ResendVerifyEmailAsync(request));
        Assert.Equal(ErrorCodes.UserAccountAlreadyDeleted, ex.Code);
    }

    [Fact]
    public async Task ResendVerifyEmailAsync_ValidRequest_SendsEmail()
    {
        var (service, mocks) = CreateAuthService(out _);
        var request = new ResendVerifyEmailRequest { Email = TestEmail };
        var user = new User { UserId = TestUserId, Email = TestEmail, Status = UserStatus.Active };

        mocks.EmailVerificationCache.Setup(x => x.CanSendVerificationEmailAsync(It.IsAny<string>())).ReturnsAsync(true);
        mocks.UserRepository.Setup(x => x.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);
        mocks.EmailVerificationCache.Setup(x => x.StoreVerificationTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<TimeSpan>())).Returns(Task.CompletedTask);
        mocks.EmailVerificationCache.Setup(x => x.IncrementSendCountAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        mocks.EmailService.Setup(x => x.SendLinkAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        mocks.Configuration.Setup(x => x["Frontend:VerifyURL"]).Returns("http://localhost:3000/verify");

        await service.ResendVerifyEmailAsync(request);

        mocks.EmailVerificationCache.Verify(x => x.StoreVerificationTokenAsync(
            TestEmail, It.IsAny<string>(), TestUserId, TimeSpan.FromMinutes(15)), Times.Once);
        mocks.EmailService.Verify(x => x.SendLinkAsync(
            TestEmail, "Xác thực tài khoản của bạn", It.IsAny<string>()), Times.Once);
    }

    // =====================================================================
    // HELPER METHODS
    // =====================================================================

    private (AuthService service, AuthServiceMocks mocks) CreateAuthService(out AuthServiceMocks mockObjects)
    {
        mockObjects = new AuthServiceMocks();

        var service = new AuthService(
            mockObjects.UserRepository.Object,
            mockObjects.PasswordHasher.Object,
            mockObjects.Configuration.Object,
            mockObjects.RefreshTokenRepository.Object,
            mockObjects.EmailService.Object,
            mockObjects.EmailVerificationCache.Object,
            mockObjects.ResetCache.Object
        );

        return (service, mockObjects);
    }

    private static HttpResponse CreateMockHttpResponse()
    {
        var httpResponse = new Mock<HttpResponse>();
        var cookies = new Mock<IResponseCookies>();
        httpResponse.Setup(x => x.Cookies).Returns(cookies.Object);
        return httpResponse.Object;
    }

    private class AuthServiceMocks
    {
        public Mock<IUserRepository> UserRepository { get; } = new();
        public Mock<IRefreshTokenRepository> RefreshTokenRepository { get; } = new();
        public Mock<IPasswordHasher<User>> PasswordHasher { get; } = new();
        public Mock<Microsoft.Extensions.Configuration.IConfiguration> Configuration { get; } = new();
        public Mock<StudioStudio_Server.Services.Interfaces.IEmailService> EmailService { get; } = new();
        public Mock<StudioStudio_Server.Services.Interfaces.IEmailVerificationCacheService> EmailVerificationCache { get; } = new();
        public Mock<StudioStudio_Server.Services.Interfaces.IPasswordResetCacheService> ResetCache { get; } = new();
    }
}