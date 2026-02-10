using Google.Apis.Auth;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using StudioStudio_Server.Configurations;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
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
    public class AuthService : IAuthService
    {
        private readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
        // Password must be 8-10 characters long, contain at least one uppercase letter, one lowercase letter, and one digit
        private readonly Regex PasswordRegex = new(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)[A-Za-z\d@$!%*?&]{8,10}$", RegexOptions.Compiled);

        private readonly IUserRepository _userRepository;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        private readonly IEmailVerificationTokenRepository _emailToken;
        public AuthService(
            IUserRepository userRepository,
            IPasswordHasher<User> passwordHasher,
            IConfiguration configuration,
            IRefreshTokenRepository refreshTokenRepository,
            IEmailService emailService,
            IEmailVerificationTokenRepository emailToken)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
            _configuration = configuration;
            _refreshTokenRepository = refreshTokenRepository;
            _emailService = emailService;
            _emailToken = emailToken;
        }
        public async Task RegisterAsync(RegisterRequests registerRequest)
        {
            if (!IsValidEmail(registerRequest.Email) || !IsValidPass(registerRequest.Password))
            {
                Console.WriteLine("Invalid email or password format");
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status400BadRequest);
            }

            //check if user email have used or not
            User? existUser = await _userRepository.GetByEmailAsync(registerRequest.Email);

            //if email used, throw exception
            if (existUser != null)
            {
                throw new AppException(ErrorCodes.UserAlreadyExist, StatusCodes.Status400BadRequest);
            }

            if (registerRequest.Password != registerRequest.ConfirmPassword)
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status400BadRequest);
            }

            //else create new user
            User registedUser = new User
            {
                UserId = Guid.NewGuid(),
                Email = registerRequest.Email,
                FirstName = registerRequest.FirstName,
                LastName = registerRequest.LastName,
                Status = UserStatus.Inactive
            };

            //hashpassword using .net PasswordHasher
            registedUser.PasswordHash = _passwordHasher.HashPassword(registedUser, registerRequest.Password);
            registedUser.CreatedAt = DateTime.UtcNow;

            await _userRepository.AddAsync(registedUser);

            //create email token for user to validate
            var emailToken = new EmailVerificationToken
            {
                Id = Guid.NewGuid(),
                UserId = registedUser.UserId,
                Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                IsUsed = false
            };

            await _emailToken.AddAsync(emailToken);

            //Fe verify code url - URL encode token to handle special characters
            string verifyUrl = $"{_configuration["Frontend:VerifyURL"]}?token={Uri.EscapeDataString(emailToken.Token)}";

            string html = EmailTemplate.VerifyLinkEmail(verifyUrl);

            await _emailService.SendLinkAsync(
                registedUser.Email,
                "Xác thực tài khoản của bạn",
                html
            );
        }

        public async Task VerifyEmailLinkAsync(string token)
        {
            var verifyToken = await _emailToken.GetValidAsync(token);
            if (verifyToken == null)
            {
                throw new UnauthorizedAccessException("Invalid token");
            }
            if (verifyToken.User.Status == UserStatus.Active)
            {
                throw new Exception("Already verified");
            }
            verifyToken.User.Status = UserStatus.Active;

            await _emailToken.MaskAsUsed(verifyToken);
        }

        public async Task<LoginResponse> LoginAsync(LoginRequests loginRequest, HttpResponse response)
        {
            if (!IsValidEmail(loginRequest.Email))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            //find user and check if user exist or not
            User? user = await _userRepository.GetByEmailAsync(loginRequest.Email);

            if (user == null)
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            if (user.Status == UserStatus.Inactive)
            {
                throw new AppException(ErrorCodes.AuthAccountNotVerified, StatusCodes.Status403Forbidden);
            }

            //check user password has match
            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, loginRequest.Password);

            if (result != PasswordVerificationResult.Success)
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            var accessTokenExpireMs = _configuration.GetValue<long>("JWT:AccessTokenExpireMs", 3600000);
            var refreshTokenExpireMs = _configuration.GetValue<long>("JWT:RefreshTokenExpireMs", 86400000);

            var accessExpireAt = DateTime.UtcNow.AddMilliseconds(accessTokenExpireMs);
            var refreshExpireAt = DateTime.UtcNow.AddMilliseconds(refreshTokenExpireMs);

            string accessToken = GenerateJWTToken(user, accessExpireAt);

            //create new refresh token each time user login
            if (user.RefreshToken != null)
            {
                await _refreshTokenRepository.RevokeAsync(user.RefreshToken);
            }

            RefreshToken refreshToken = CreateRefreshToken(user, refreshExpireAt);
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
                RefreshExpireIn = refreshTokenExpireMs
            };
        }

        private string GenerateJWTToken(User user, DateTime expireAt)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWT:Key"]));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["JWT:Issuer"],
                audience: _configuration["JWT:Audience"],
                claims: claims,
                expires: expireAt,
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

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

        public async Task<LoginResponse> RefreshTokenAsync(string refreshToken, HttpResponse response)
        {
            var token = await _refreshTokenRepository.GetValidAsync(refreshToken);

            if (token == null || token.IsRevoked || token.ExpiresAt < DateTime.UtcNow)
                throw new AppException(ErrorCodes.AuthTokenExpired, StatusCodes.Status401Unauthorized);

            await _refreshTokenRepository.RevokeAsync(token);

            var user = await _userRepository.GetByIdAsync(token.UserId);
            if (user == null)
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);

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
                RefreshExpireIn = refreshTokenExpireMs
            };
        }

        public async Task LogoutAsync(string refreshToken, HttpResponse response)
        {
            var token = await _refreshTokenRepository.GetValidAsync(refreshToken);
            if (token != null)
            {
                await _refreshTokenRepository.RevokeAsync(token);
            }

            response.Cookies.Delete("refreshToken", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None
            });
        }

        public async Task<LoginResponse> GoogleLoginAsync(GoogleLoginRequest request, HttpResponse response)
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
                user = new User
                {
                    UserId = Guid.NewGuid(),
                    Email = email,
                    GoogleId = googleId,
                    FirstName = firstName,
                    LastName = lastName,
                    AvatarUrl = imgURL,
                    Status = UserStatus.Active,
                };

                await _userRepository.AddAsync(user);
            }
            else
            {
                user.GoogleId ??= googleId;
                user.FirstName ??= firstName;
                user.LastName ??= lastName;
                user.AvatarUrl ??= imgURL;
                user.Status = UserStatus.Active;

                await _userRepository.UpdateAsync(user);
            }

            if (user.RefreshToken != null)
            {
                await _refreshTokenRepository.RevokeAsync(user.RefreshToken);
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
                RefreshExpireIn = refreshTokenExpireMs
            };
        }

        public async Task SendResetPasswordLinkAsync(string email)
        {
            if (!IsValidEmail(email))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status400BadRequest);
            }

            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null)
            {
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
            }

            var token = new EmailVerificationToken
            {
                Id = Guid.NewGuid(),
                UserId = user.UserId,
                Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
                IsUsed = false,
            };

            await _emailToken.AddAsync(token);

            string resetURL = $"{_configuration["Frontend:ResetPassURL"]}?token={Uri.EscapeDataString(token.Token)}";
            string html = EmailTemplate.ResetPasswordEmail(resetURL);

            await _emailService.SendLinkAsync(
                user.Email,
                "Reset your password",
                html
            );
        }

        private bool IsValidEmail(string email)
        {
            return EmailRegex.IsMatch(email);
        }

        private bool IsValidPass(string pass)
        {
            return PasswordRegex.IsMatch(pass);
        }
    }
}
