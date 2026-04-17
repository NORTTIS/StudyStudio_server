using Google.Apis.Auth;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using StudioStudio_Server.Configurations;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Metrics;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service handling authentication and authorization logic
    /// Supports: Register, Login, Logout, Google OAuth, Password Reset, Email Verification
    /// Security: JWT (Access Token) + Refresh Token with HttpOnly Cookies
    /// </summary>
    public class AuthService(
        IUserRepository userRepository,
        IPasswordHasher<User> passwordHasher,
        IConfiguration configuration,
        IRefreshTokenRepository refreshTokenRepository,
        IEmailService emailService,
        IEmailVerificationCacheService emailVerificationCache,
        IPasswordResetCacheService resetCache) : IAuthService
    {
        private readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        // Password must be 10-20 characters long, contain at least one uppercase letter, one lowercase letter, and one digit
        private readonly Regex PasswordRegex = new(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)[A-Za-z\d@$!%*?&]{10,20}$", RegexOptions.Compiled);

        private readonly IUserRepository _userRepository = userRepository;
        private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
        private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;
        private readonly IConfiguration _configuration = configuration;
        private readonly IEmailService _emailService = emailService;
        private readonly IEmailVerificationCacheService _emailVerificationCache = emailVerificationCache;
        private readonly IPasswordResetCacheService _resetCache = resetCache;

        /// <summary>
        /// Register new user account
        /// Validate:
        /// - Email format and uniqueness
        /// - Password strength (10-20 chars, uppercase, lowercase, digit)
        /// - Rate limit (5 emails per 15 minutes)
        /// Flow:
        /// 1. Create user with Status = Inactive
        /// 2. Generate verification token (stored in Redis)
        /// 3. Send verification email
        /// </summary>
        public async Task RegisterAsync(RegisterRequests registerRequest)
        {
            if (!IsValidEmail(registerRequest.Email))
            {
                throw new AppException(ErrorCodes.ValidationInvalidEmail, StatusCodes.Status400BadRequest);
            }

            if (!IsValidPass(registerRequest.Password))
            {
                throw new AppException(ErrorCodes.ValidationInvalidPassword, StatusCodes.Status400BadRequest);
            }

            // Check rate limit
            if (!await _emailVerificationCache.CanSendVerificationEmailAsync(registerRequest.Email))
            {
                throw new AppException(ErrorCodes.ValidationRateLimitExceeded, StatusCodes.Status429TooManyRequests);
            }

            //check if user email have used or not
            User? existUser = await _userRepository.GetByEmailAsync(registerRequest.Email);

            //if email used, throw exception
            if (existUser != null)
            {
                throw new AppException(ErrorCodes.UserAlreadyExist, StatusCodes.Status400BadRequest);
            }

            //else create new user
            User registedUser = new User
            {
                UserId = Guid.NewGuid(),
                Email = registerRequest.Email,
                FirstName = registerRequest.FirstName,
                LastName = registerRequest.LastName,
                Status = UserStatus.Active,
                IsVerify = false
            };

            //hashpassword using .net PasswordHasher
            registedUser.PasswordHash = _passwordHasher.HashPassword(registedUser, registerRequest.Password);
            registedUser.CreatedAt = DateTime.UtcNow;

            await _userRepository.AddAsync(registedUser);

            // Record registration metric
            AppMetrics.UserRegistrationsTotal.Inc();

            // Generate verification token and store in Redis
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            var expiry = TimeSpan.FromMinutes(15);

            await _emailVerificationCache.StoreVerificationTokenAsync(
                registerRequest.Email,
                token,
                registedUser.UserId,
                expiry
            );

            // Increment rate limit counter
            await _emailVerificationCache.IncrementSendCountAsync(registerRequest.Email);

            //Fe verify code url - URL encode token to handle special characters
            string verifyUrl = $"{_configuration["Frontend:VerifyURL"]}?token={Uri.EscapeDataString(token)}";

            string html = EmailTemplate.VerifyLinkEmail(verifyUrl);

            await _emailService.SendLinkAsync(
                registedUser.Email,
                "Xác thực tài khoản của bạn",
                html
            );
        }

        /// <summary>
        /// Verify email using token from verification link
        /// Validate:
        /// - Token exists and not expired (from Redis)
        /// - User exists and not deleted
        /// - Email not already verified
        /// Action: Set user Status = Active
        /// </summary>
        public async Task VerifyEmailLinkAsync(string token)
        {
            var verifyData = await _emailVerificationCache.GetVerificationDataByTokenAsync(token);

            if (verifyData == null)
            {
                throw new AppException(ErrorCodes.ValidationInvalidToken, StatusCodes.Status400BadRequest);
            }

            var user = await _userRepository.GetByIdAsync(verifyData.UserId);

            if (user == null)
            {
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
            }

            if (user.Status == UserStatus.Deleted)
            {
                throw new AppException(ErrorCodes.UserAccountAlreadyDeleted, StatusCodes.Status400BadRequest);
            }

            if (user.Status == UserStatus.Inactive)
            {
                throw new AppException(ErrorCodes.AuthAccountInactive, StatusCodes.Status403Forbidden);
            }

            if (user.IsVerify)
            {
                throw new AppException(ErrorCodes.ValidationEmailAlreadyVerified, StatusCodes.Status400BadRequest);
            }


            user.IsVerify = true;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user);

            // Invalidate token after successful verification
            await _emailVerificationCache.InvalidateVerificationTokenAsync(verifyData.Email);
        }

        /// <summary>
        /// Login with email and password
        /// Validate:
        /// - Email format
        /// - User exists and not deleted
        /// - Password matches
        /// - Account is active (email verified)
        /// Returns: JWT Access Token + Refresh Token (in HttpOnly cookie)
        /// </summary>
        public async Task<LoginResponse> LoginAsync(LoginRequests loginRequest, HttpResponse response)
        {
            if (!IsValidEmail(loginRequest.Email))
            {
                throw new AppException(ErrorCodes.ValidationInvalidEmail, StatusCodes.Status400BadRequest);
            }

            //find user and check if user exist or not
            User? user = await _userRepository.GetByEmailAsync(loginRequest.Email);

            if (user == null)
            {
                AppMetrics.UserLoginAttemptsTotal.WithLabels("failed").Inc();
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            if (user.Status == UserStatus.Deleted)
            {
                AppMetrics.UserLoginAttemptsTotal.WithLabels("failed").Inc();
                throw new AppException(ErrorCodes.UserAccountAlreadyDeleted, StatusCodes.Status400BadRequest);
            }

            //check user password has match
            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash!, loginRequest.Password);

            if (result != PasswordVerificationResult.Success)
            {
                AppMetrics.UserLoginAttemptsTotal.WithLabels("failed").Inc();
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            if (user.Status == UserStatus.Inactive)
            {
                throw new AppException(ErrorCodes.AuthAccountInactive, StatusCodes.Status403Forbidden);
            }

            if (!user.IsVerify)
            {
                throw new AppException(ErrorCodes.AuthAccountNotVerified, StatusCodes.Status403Forbidden);
            }

            var accessTokenExpireMs = _configuration.GetValue<long>("JWT:AccessTokenExpireMs", 3600000);
            var refreshTokenExpireMs = _configuration.GetValue<long>("JWT:RefreshTokenExpireMs", 86400000);

            var accessExpireAt = DateTime.UtcNow.AddMilliseconds(accessTokenExpireMs);
            var refreshExpireAt = DateTime.UtcNow.AddMilliseconds(refreshTokenExpireMs);

            string accessToken = GenerateJWTToken(user, accessExpireAt);

            RefreshToken refreshToken = CreateRefreshToken(user, refreshExpireAt);
            await _refreshTokenRepository.AddAsync(refreshToken);

            SetRefreshTokenCookie(response, refreshToken.Token, refreshExpireAt);

            // Record login metrics
            AppMetrics.UserLoginAttemptsTotal.WithLabels("success").Inc();
            AppMetrics.ActiveJwtTokens.Inc();

            return new LoginResponse
            {
                Id = user.UserId,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                AccessToken = accessToken,
                AccessExpireIn = accessTokenExpireMs,
                RefreshToken = refreshToken.Token,
                RefreshExpireIn = refreshTokenExpireMs,
                IsAdmin = user.IsAdmin,
                AvatarUrl = user.AvatarUrl
            };
        }

        /// <summary>
        /// Refresh access token using refresh token
        /// Validate:
        /// - Refresh token exists, not revoked, and not expired
        /// - User exists and not deleted
        /// Flow:
        /// 1. Revoke old refresh token
        /// 2. Cleanup expired/revoked tokens for this user
        /// 3. Generate new access token + new refresh token
        /// 4. Return new tokens
        /// Security: Token rotation - old refresh token cannot be reused
        /// OPTIMIZATION: Auto-cleanup prevents token accumulation
        /// </summary>
        public async Task<LoginResponse> RefreshTokenAsync(string refreshToken, HttpResponse response)
        {
            var token = await _refreshTokenRepository.GetValidAsync(refreshToken);

            if (token == null || token.IsRevoked || token.ExpiresAt < DateTime.UtcNow)
                throw new AppException(ErrorCodes.AuthTokenExpired, StatusCodes.Status401Unauthorized);

            await _refreshTokenRepository.RevokeAsync(token);

            // ✅ CLEANUP: Delete all expired/revoked tokens for this user
            await _refreshTokenRepository.CleanupUserTokensAsync(token.UserId);

            var user = await _userRepository.GetByIdAsync(token.UserId);
            if (user == null)
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);

            if (user.Status == UserStatus.Deleted)
                throw new AppException(ErrorCodes.UserAccountAlreadyDeleted, StatusCodes.Status400BadRequest);

            var accessTokenExpireMs = _configuration.GetValue<long>("JWT:AccessTokenExpireMs", 3600000);
            var refreshTokenExpireMs = _configuration.GetValue<long>("JWT:RefreshTokenExpireMs", 86400000);

            var accessExpireAt = DateTime.UtcNow.AddMilliseconds(accessTokenExpireMs);
            var refreshExpireAt = DateTime.UtcNow.AddMilliseconds(refreshTokenExpireMs);

            var newRefreshToken = CreateRefreshToken(user, refreshExpireAt);
            await _refreshTokenRepository.AddAsync(newRefreshToken);

            var newAccessToken = GenerateJWTToken(user, accessExpireAt);

            SetRefreshTokenCookie(response, newRefreshToken.Token, refreshExpireAt);

            return new LoginResponse
            {
                Id = user.UserId,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                AccessToken = newAccessToken,
                AccessExpireIn = accessTokenExpireMs,
                RefreshToken = newRefreshToken.Token,
                RefreshExpireIn = refreshTokenExpireMs,
                IsAdmin = user.IsAdmin,
                AvatarUrl = user.AvatarUrl
            };
        }

        /// <summary>
        /// Logout user
        /// Action:
        /// 1. Revoke refresh token in database
        /// 2. Cleanup expired/revoked tokens for this user
        /// 3. Delete refresh token cookie
        /// OPTIMIZATION: Auto-cleanup prevents token accumulation
        /// </summary>
        public async Task LogoutAsync(string refreshToken, HttpResponse response)
        {
            var token = await _refreshTokenRepository.GetValidAsync(refreshToken);
            if (token != null)
            {
                await _refreshTokenRepository.RevokeAsync(token);

                // ✅ CLEANUP: Delete all expired/revoked tokens for this user
                await _refreshTokenRepository.CleanupUserTokensAsync(token.UserId);
            }

            response.Cookies.Delete("refreshToken", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None
            });
        }

        /// <summary>
        /// Login with Google OAuth
        /// Validate: Google ID token
        /// Flow:
        /// 1. Verify Google ID token
        /// 2. If user doesn't exist → create new user with Status = Active (no email verification needed)
        /// 3. If user exists → update Google info if missing
        /// 4. Generate JWT tokens
        /// Returns: JWT Access Token + Refresh Token (in HttpOnly cookie)
        /// </summary>
        public async Task<LoginResponse> GoogleLoginAsync(GoogleLoginRequest request, HttpResponse response)
        {
            try
            {
                var settings = new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { _configuration["Google:ClientId"] }
                };

                var payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);

                var email = payload.Email;
                var googleId = payload.Subject;
                var firstName = payload.GivenName;
                var lastName = payload.FamilyName;
                var imgURL = payload.Picture;

                var user = await _userRepository.GetByEmailAsync(email);

            
                if (user == null)
                {
                    var tempPassword = Guid.NewGuid().ToString();
                    var passwordHash = _passwordHasher.HashPassword(null!, tempPassword);
                    user = new User
                    {
                        UserId = Guid.NewGuid(),
                        Email = email,
                        PasswordHash = passwordHash,
                        GoogleId = googleId,
                        FirstName = firstName,
                        LastName = lastName,
                        AvatarUrl = imgURL,
                        Status = UserStatus.Active,
                        IsVerify = true
                    };

                    await _userRepository.AddAsync(user);
                }
                else
                {
                    if(user.Status == UserStatus.Inactive)
                    {
                        throw new AppException(ErrorCodes.AuthAccountInactive);
                    }

                    user.GoogleId ??= googleId;
                    user.AvatarUrl ??= imgURL;

                    await _userRepository.UpdateAsync(user);
                }

                var accessTokenExpireMs = _configuration.GetValue<long>("JWT:AccessTokenExpireMs", 3600000);
                var refreshTokenExpireMs = _configuration.GetValue<long>("JWT:RefreshTokenExpireMs", 86400000);

                var accessExpireAt = DateTime.UtcNow.AddMilliseconds(accessTokenExpireMs);
                var refreshExpireAt = DateTime.UtcNow.AddMilliseconds(refreshTokenExpireMs);

                var accessToken = GenerateJWTToken(user, accessExpireAt);
                var refreshToken = CreateRefreshToken(user, refreshExpireAt);

                await _refreshTokenRepository.AddAsync(refreshToken);

                SetRefreshTokenCookie(response, refreshToken.Token, refreshExpireAt);

                return new LoginResponse
                {
                    Id = user.UserId,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    AccessToken = accessToken,
                    AccessExpireIn = accessTokenExpireMs,
                    RefreshToken = refreshToken.Token,
                    RefreshExpireIn = refreshTokenExpireMs,
                    IsAdmin = user.IsAdmin,
                    AvatarUrl = user.AvatarUrl
                };
            }
            catch (AppException)
            {
                throw;
            }
            catch (Exception)
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }
        }

        /// <summary>
        /// Send password reset link to user email
        /// Validate:
        /// - Email format and exists
        /// - User not deleted
        /// - Rate limit (5 emails per 15 minutes)
        /// Flow:
        /// 1. Generate reset token (stored in Redis with 15-min expiry)
        /// 2. Send reset password email with link
        /// </summary>
        public async Task SendResetPasswordLinkAsync(string email)
        {
            if (!IsValidEmail(email))
            {
                throw new AppException(ErrorCodes.ValidationInvalidEmail, StatusCodes.Status400BadRequest);
            }

            // Check rate limit
            if (!await _resetCache.CanSendResetEmailAsync(email))
            {
                throw new AppException(ErrorCodes.ValidationRateLimitExceeded, StatusCodes.Status429TooManyRequests);
            }

            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null)
            {
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
            }

            if (user.Status == UserStatus.Deleted)
            {
                throw new AppException(ErrorCodes.UserAccountAlreadyDeleted, StatusCodes.Status400BadRequest);
            }

            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            var expiry = TimeSpan.FromMinutes(15);

            await _resetCache.StoreResetTokenAsync(email, token, user.UserId, expiry);

            // Increment rate limit counter
            await _resetCache.IncrementSendCountAsync(email);

            string resetURL = $"{_configuration["Frontend:ResetPassURL"]}?token={Uri.EscapeDataString(token)}";
            string html = EmailTemplate.ResetPasswordEmail(resetURL);

            await _emailService.SendLinkAsync(
                user.Email,
                "Reset your password",
                html
            );
        }

        /// <summary>
        /// Reset user password using token from reset link
        /// Validate:
        /// - Token exists and not expired (from Redis)
        /// - Password strength
        /// - User exists and not deleted
        /// Action:
        /// 1. Update password hash
        /// 2. Invalidate reset token
        /// 3. Revoke all existing refresh tokens (force re-login on all devices)
        /// </summary>
        public async Task ResetPasswordAsync(string token, string newPassword)
        {
            if (!IsValidPass(newPassword))
            {
                throw new AppException(ErrorCodes.ValidationInvalidPassword, StatusCodes.Status400BadRequest);
            }

            var resetData = await _resetCache.GetResetDataByTokenAsync(token);

            if (resetData == null)
            {
                throw new AppException(ErrorCodes.ValidationInvalidToken, StatusCodes.Status400BadRequest);
            }

            var user = await _userRepository.GetByIdAsync(resetData.UserId);
            if (user == null)
            {
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
            }

            if (user.Status == UserStatus.Deleted)
            {
                throw new AppException(ErrorCodes.UserAccountAlreadyDeleted, StatusCodes.Status400BadRequest);
            }

            user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user);
            await _resetCache.InvalidateResetTokenAsync(resetData.Email);

            await _refreshTokenRepository.RevokeAllUserTokensAsync(user.UserId);
        }

        /// <summary>
        /// Verify if reset token is valid
        /// Used to check token validity before showing reset password form
        /// Returns: true if token is valid and user exists
        /// </summary>
        public async Task<bool> VerifyResetTokenAsync(string token)
        {
            var resetData = await _resetCache.GetResetDataByTokenAsync(token);

            if (resetData != null)
            {
                var user = await _userRepository.GetByIdAsync(resetData.UserId);
                if (user == null || user.Status == UserStatus.Deleted)
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Resend email verification link
        /// Validate:
        /// - Email format and exists
        /// - User not deleted
        /// - Email not already verified
        /// - Rate limit (5 emails per 15 minutes)
        /// Flow: Same as Register (generate new token + send email)
        /// </summary>
        public async Task ResendVerifyEmailAsync(ResendVerifyEmailRequest request)
        {
            if (!IsValidEmail(request.Email))
            {
                throw new AppException(
                    ErrorCodes.ValidationInvalidEmail,
                    StatusCodes.Status400BadRequest
                );
            }

            // Check rate limit
            if (!await _emailVerificationCache.CanSendVerificationEmailAsync(request.Email))
            {
                throw new AppException(ErrorCodes.ValidationRateLimitExceeded, StatusCodes.Status429TooManyRequests);
            }

            var user = await _userRepository.GetByEmailAsync(request.Email);

            if (user == null)
            {
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
            }

            if (user.Status == UserStatus.Deleted)
            {
                throw new AppException(ErrorCodes.UserAccountAlreadyDeleted, StatusCodes.Status400BadRequest);
            }


            // Generate new token and store in Redis
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            var expiry = TimeSpan.FromMinutes(15);

            await _emailVerificationCache.StoreVerificationTokenAsync(
                request.Email,
                token,
                user.UserId,
                expiry
            );

            // Increment rate limit counter
            await _emailVerificationCache.IncrementSendCountAsync(request.Email);

            string verifyUrl =
                $"{_configuration["Frontend:VerifyURL"]}?token={Uri.EscapeDataString(token)}";

            string html = EmailTemplate.VerifyLinkEmail(verifyUrl);

            await _emailService.SendLinkAsync(
                user.Email,
                "Xác thực tài khoản của bạn",
                html
            );
        }

        /// <summary>
        /// Validate email format using regex
        /// </summary>
        private bool IsValidEmail(string email)
        {
            return EmailRegex.IsMatch(email);
        }

        /// <summary>
        /// Validate password strength
        /// Requirements: 10-20 chars, at least one uppercase, one lowercase, one digit
        /// </summary>
        private bool IsValidPass(string pass)
        {
            return PasswordRegex.IsMatch(pass);
        }

        /// <summary>
        /// Generate JWT access token
        /// Claims: UserId (Sub), Email, IsAdmin
        /// Algorithm: HMAC SHA256
        /// </summary>
        private string GenerateJWTToken(User user, DateTime expireAt)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim("IsAdmin", user.IsAdmin.ToString())
            };

            var jwtKey = _configuration["JWT:Key"]
                ?? throw new InvalidOperationException("JWT:Key is not configured");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["JWT:Issuer"],
                audience: _configuration["JWT:Audience"],
                claims: claims,
                expires: expireAt,
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// Create new refresh token entity
        /// Token: Random 64-byte base64 string
        /// </summary>
        private RefreshToken CreateRefreshToken(User user, DateTime expireAt)
        {
            return new RefreshToken
            {
                Id = Guid.NewGuid(),
                Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                ExpiresAt = expireAt,
                UserId = user.UserId
            };
        }

        /// <summary>
        /// Set refresh token in HttpOnly cookie
        /// Security: HttpOnly prevents XSS, Secure requires HTTPS, SameSite=None for cross-origin
        /// </summary>
        private void SetRefreshTokenCookie(HttpResponse response, string token, DateTime expireAt)
        {
            response.Cookies.Append("refreshToken", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = expireAt
            });
        }
    }
}
