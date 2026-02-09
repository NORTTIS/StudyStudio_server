using Google.Apis.Auth;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using StudioStudio_Server.Configurations;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
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
    public class AuthService : IAuthService
    {
        private readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
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
        public async Task RegisterAsync(RegisterRequests request)
        {
            if (!IsValidEmail(request.Email) || !IsValidPass(request.Password))
                throw new Exception("Invalid input");

            if (await _userRepository.GetByEmailAsync(request.Email) != null)
                throw new Exception("Email already exists");

            var user = new User
            {
                UserId = Guid.NewGuid(),
                Email = request.Email,
                FullName = request.FirstName + " " + request.LastName,
                Status = UserStatus.Inactive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            await _userRepository.AddAsync(user);

            var token = new EmailVerificationToken
            {
                Id = Guid.NewGuid(),
                UserId = user.UserId,
                Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                Type = EmailTokenType.VerifyEmail,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5)
            };

            await _emailToken.AddAsync(token);

            var url = $"{_configuration["Frontend:VerifyURL"]}?token={token.Token}";
            await _emailService.SendLinkAsync(user.Email, "Verify account", EmailTemplate.VerifyLinkEmail(url));
        }


        public async Task VerifyEmailAsync(string token)
        {
            var verifyToken = await _emailToken.GetValidAsync(token, EmailTokenType.VerifyEmail);
            if (verifyToken == null)
                throw new UnauthorizedAccessException("Invalid token");

            verifyToken.User.Status = UserStatus.Active;
            verifyToken.User.UpdatedAt = DateTime.UtcNow;

            await _emailToken.MarkAsUsedAsync(verifyToken);
        }

        public async Task<string> LoginAsync(LoginRequests request, HttpResponse response)
        {
            var user = await _userRepository.GetByEmailAsync(request.Email);
            if (user == null)
                throw new Exception("Invalid credentials");

            if (_passwordHasher.VerifyHashedPassword(user, user.PasswordHash!, request.Password)
                != PasswordVerificationResult.Success)
                throw new Exception("Invalid credentials");

            var accessToken = GenerateJWTToken(user);

            var refreshToken = CreateRefreshToken(user);
            await _refreshTokenRepository.AddAsync(refreshToken);

            SetRefreshTokenCookie(response, refreshToken.Token);

            return accessToken;
        }


        private string GenerateJWTToken(User user)
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
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private RefreshToken CreateRefreshToken(User user)
        {
            return new RefreshToken
            {
                Id = Guid.NewGuid(),
                Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                UserId = user.UserId
            };
        }

        private void SetRefreshTokenCookie(HttpResponse response, string token)
        {
            response.Cookies.Append("refreshToken", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTime.UtcNow.AddDays(7)
            });
        }

        public async Task<string> RefreshTokenAsync(string refreshToken, HttpResponse response)
        {
            var token = await _refreshTokenRepository.GetValidAsync(refreshToken);
            if (token == null)
                throw new UnauthorizedAccessException("Invalid refresh token");

            await _refreshTokenRepository.RevokeAsync(token);

            var newRefreshToken = CreateRefreshToken(token.User);
            await _refreshTokenRepository.AddAsync(newRefreshToken);

            SetRefreshTokenCookie(response, newRefreshToken.Token);

            return GenerateJWTToken(token.User);
        }


        public async Task LogoutAsync(string refreshToken, HttpResponse response)
        {
            var token = await _refreshTokenRepository.GetValidAsync(refreshToken);
            if (token != null)
                await _refreshTokenRepository.RevokeAsync(token);

            response.Cookies.Delete("refreshToken");
        }


        public async Task<string> GoogleLoginAsync(GoogleLoginRequest request, HttpResponse response)
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _configuration["Google:ClientId"] }
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);

            var email = payload.Email;
            var googleId = payload.Subject;
            var fullName = $"{payload.GivenName} {payload.FamilyName}";
            var avatarUrl = payload.Picture;

            var user = await _userRepository.GetByEmailAsync(email);

            if (user == null)
            {
                user = new User
                {
                    UserId = Guid.NewGuid(),
                    Email = email,
                    GoogleId = googleId,
                    FullName = fullName,
                    AvatarUrl = avatarUrl,
                    Status = UserStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _userRepository.AddAsync(user);
            }
            else
            {
                user.GoogleId ??= googleId;
                user.FullName = fullName;
                user.AvatarUrl = avatarUrl;
                user.Status = UserStatus.Active;
                user.UpdatedAt = DateTime.UtcNow;

                await _userRepository.UpdateAsync(user);

                // REVOKE TẤT CẢ refresh token cũ
                var activeTokens = await _refreshTokenRepository.GetActiveByUserIdAsync(user.UserId);
                foreach (var t in activeTokens)
                {
                    await _refreshTokenRepository.RevokeAsync(t);
                }
            }

            // tạo token mới
            var accessToken = GenerateJWTToken(user);

            var refreshToken = CreateRefreshToken(user);
            await _refreshTokenRepository.AddAsync(refreshToken);

            SetRefreshTokenCookie(response, refreshToken.Token);

            return accessToken;
        }


        public async Task SendResetPasswordLinkAsync(string email)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null) return; // tránh lộ user tồn tại

            var token = new EmailVerificationToken
            {
                Id = Guid.NewGuid(),
                UserId = user.UserId,
                Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                Type = EmailTokenType.ResetPassword,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5)
            };

            await _emailToken.AddAsync(token);

            var url = $"{_configuration["Frontend:ResetPassURL"]}?token={token.Token}";
            await _emailService.SendLinkAsync(user.Email, "Reset password", EmailTemplate.ResetPasswordEmail(url));
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
