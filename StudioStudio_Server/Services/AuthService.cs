using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace StudioStudio_Server.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IConfiguration _configuration;
        public AuthService(
            IUserRepository userRepository,
            IPasswordHasher<User> passwordHasher,
            IConfiguration configuration,
            IRefreshTokenRepository refreshTokenRepository)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
            _configuration = configuration;
            _refreshTokenRepository = refreshTokenRepository;
        }
        public async Task RegisterAsync(Models.DTOs.RegisterRequest registerRequest)
        {
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
                FullName = registerRequest.FirstName + " " + registerRequest.LastName
            };

            //hashpassword using .net PasswordHasher
            registedUser.PasswordHash = _passwordHasher.HashPassword(registedUser, registerRequest.Password);
            registedUser.CreatedAt = DateTime.UtcNow;

            await _userRepository.AddAsync(registedUser);
        }
        public async Task<string> LoginAsync(Models.DTOs.LoginRequest loginRequest, HttpResponse response)
        {
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
                SameSite = SameSiteMode.Strict,
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
        public async Task LogoutAsync(string refreshToken)
        {
            var token = await _refreshTokenRepository.GetValidAsync(refreshToken);
            if (token != null)
            {
                await _refreshTokenRepository.RevokeAsync(token);
            }
        }
    }
}
