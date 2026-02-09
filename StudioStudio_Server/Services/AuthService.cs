using Google.Apis.Auth;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using StudioStudio_Server.Configurations;
using StudioStudio_Server.Models.DTOs;
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
        private readonly Regex PasswordRegex = new(@"""^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,10}$", RegexOptions.Compiled);

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
        public async Task RegisterAsync(Models.DTOs.RegisterRequest registerRequest)
        {
            if (!IsValidEmail(registerRequest.Email))
            {
                throw new Exception("This email address does not in right format");
            }

            if (!IsValidPass(registerRequest.Password))
            {
                throw new Exception("Password need to have at least 8 letters, 1 uppercase, 1 special character");
            }

            //check if user email have used or not
            User? existUser = await _userRepository.GetByEmailAsync(registerRequest.Email);

            //if email used, throw exception
            if (existUser != null)
            {
                throw new Exception("This email address has already been registered");
            }

            if (registerRequest.Password != registerRequest.ConfirmPassword)
            {
                throw new Exception("Password and confirmation password do not match");
            }

            //else create new user
            User registedUser = new User
            {
                UserId = Guid.NewGuid(),
                Email = registerRequest.Email,
                FullName = registerRequest.FirstName + " " + registerRequest.LastName,
                Status = UserStatus.Inactive
            };

            //hashpassword using .net PasswordHasher
            registedUser.PasswordHash = _passwordHasher.HashPassword(registedUser, registerRequest.Password);
            registedUser.CreatedAt = DateTime.UtcNow;

            await _userRepository.AddAsync(registedUser);

            //create eamil token for user to validate
            var emailToken = new EmailVerificationToken
            {
                Id = Guid.NewGuid(),
                UserId = registedUser.UserId,
                Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            };

            await _emailToken.AddAsync(emailToken);

            //Fe verify code url
            string verifyUrl = $"{_configuration["Frontend:VerfyURL"]}?token={emailToken.Token}";

            string html = EmailTemplate.VerifyLinkEmail(verifyUrl);

            await _emailService.SendLinkAsync(
                registedUser.Email,
                "verify your account",
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

        public async Task<string> LoginAsync(Models.DTOs.LoginRequest loginRequest, HttpResponse response)
        {
            if (!IsValidEmail(loginRequest.Email))
            {
                throw new Exception("Invalid email or password");
            }

            //find user and check if user exist or not
            User? user = await _userRepository.GetByEmailAsync(loginRequest.Email);

            if (user == null)
            {
                throw new Exception("Invalid email or password");
            }

            //check user password has match
            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, loginRequest.Password);

            if (result != PasswordVerificationResult.Success)
            {
                throw new Exception("Invalid email or password");
            }

            //get access token by JWT
            string accessToken = GenerateJWTToken(user);

            //create new refresh token each time user login
            if (user.RefreshToken != null)
            {
                user.RefreshToken.IsRevoked = true;
            }

            RefreshToken refreshToken = CreateRefreshToken(user);
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

            if (token == null || token.IsRevoked || token.ExpiresAt < DateTime.UtcNow)
                throw new UnauthorizedAccessException("Invalid refresh token");

            await _refreshTokenRepository.RevokeAsync(token);

            var user = await _userRepository.GetByIdAsync(token.UserId);
            if (user == null)
                throw new UnauthorizedAccessException("User not found");

            var newRefreshToken = CreateRefreshToken(user);
            await _refreshTokenRepository.AddAsync(newRefreshToken);

            var newAccessToken = GenerateJWTToken(user);

            SetRefreshTokenCookie(response, newRefreshToken.Token);

            return newAccessToken;
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

        public async Task<string> GoogleLoginAsync(GoogleLoginRequest request, HttpResponse response)
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _configuration["Google:ClientId"] }
            };
            var payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);

            var email = payload.Email;
            var googleId = payload.Subject;
            var fullName = payload.GivenName + " " + payload.FamilyName;
            var imgURL = payload.Picture;

            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null)
            {
                user = new User
                {
                    UserId = Guid.NewGuid(),
                    Email = email,
                    GoogleId = googleId,
                    FullName = fullName,
                    AvatarUrl = imgURL,
                    Status = UserStatus.Active,
                };

                await _userRepository.AddAsync(user);
            }
            else
            {
                user.GoogleId ??= googleId;
                user.FullName ??= fullName;
                user.AvatarUrl ??= imgURL;
                user.Status = UserStatus.Active;

                await _userRepository.UpdateAsync(user);
            }

            if (user.RefreshToken != null)
            {
                await _refreshTokenRepository.RevokeAsync(user.RefreshToken);
            }

            var accessToken = GenerateJWTToken(user);
            var refreshToken = CreateRefreshToken(user);

            await _refreshTokenRepository.AddAsync(refreshToken);

            SetRefreshTokenCookie(response, refreshToken.Token);

            return accessToken;
        }

        public async Task SendResetPasswordLinkAsync(string email)
        {
            if (!IsValidEmail(email))
            {
                throw new Exception("email not in right format");
            }

            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null)
            {
                throw new Exception("this user does not exist in the system");
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

            string resetURL = $"{_configuration["Frontend:ResetPassURL"]}?token={token.Token}";
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
