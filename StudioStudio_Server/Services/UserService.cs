using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using StudioStudio_Server.Exceptions;
using System.Text.RegularExpressions;

namespace StudioStudio_Server.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IWebHostEnvironment _environment;
        // Password must be 10-20 characters long, contain at least one uppercase letter, one digit, and one special character
        private readonly Regex PasswordRegex = new(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{10,20}$", RegexOptions.Compiled);

        public UserService(
            IUserRepository userRepository,
            IPasswordHasher<User> passwordHasher,
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            IWebHostEnvironment environment)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _environment = environment;
        }

        public async Task<User?> GetByIdAsync(Guid userId)
        {
            return await _userRepository.GetByIdAsync(userId);
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _userRepository.GetByEmailAsync(email);
        }

        public async Task UpdateAsync(User user)
        {
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);
        }

        public async Task DeleteAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user != null)
            {
                user.Status = UserStatus.Deleted;
                user.UpdatedAt = DateTime.UtcNow;
                await _userRepository.UpdateAsync(user);
            }
        }

        public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
        {
            // Get user
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
            }

            // Verify current password
            var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
            if (verifyResult != PasswordVerificationResult.Success)
            {
                throw new AppException(ErrorCodes.AuthIncorrectCurrentPassword, StatusCodes.Status401Unauthorized);
            }

            // Validate new password format
            if (!IsValidPassword(request.NewPassword))
            {
                throw new AppException(ErrorCodes.ValidationInvalidPassword, StatusCodes.Status400BadRequest);
            }

            // Check password confirmation match
            if (request.NewPassword != request.ConfirmPassword)
            {
                throw new AppException(ErrorCodes.ValidationPasswordMismatch, StatusCodes.Status400BadRequest);
            }



            // Check if new password is the same as current password
            var isSamePassword = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.NewPassword);
            if (isSamePassword == PasswordVerificationResult.Success)
            {
                throw new AppException(ErrorCodes.ValidationNewPasswordSameAsCurrent, StatusCodes.Status400BadRequest);
            }

            // Update password
            user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);
        }

        public async Task UpdateProfileAsync(Guid userId, UpdateUserProfileRequest request)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
            }

            if (!string.IsNullOrWhiteSpace(request.FirstName))
                user.FirstName = request.FirstName;

            if (!string.IsNullOrWhiteSpace(request.LastName))
                user.LastName = request.LastName;

            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
                user.PhoneNumber = request.PhoneNumber;

            if (!string.IsNullOrWhiteSpace(request.Bio))
                user.Bio = request.Bio;

            if (!string.IsNullOrWhiteSpace(request.Language))
                user.Language = request.Language;

            if (request.EmailNotificationEnabled.HasValue)
                user.EmailNotificationEnabled = request.EmailNotificationEnabled.Value;

            if (request.Avatar != null)
            {
                user.AvatarUrl = await SaveAvatarAsync(userId, request.Avatar, user.AvatarUrl);
            }

            user.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);
        }

        private bool IsValidPassword(string password)
        {
            return PasswordRegex.IsMatch(password);
        }

        private async Task<string> SaveAvatarAsync(Guid userId, IFormFile file, string? existingAvatarUrl)
        {
            if (file.Length > 5 * 1024 * 1024) // 5MB limit
            {
                throw new AppException(ErrorCodes.ValidationFileSizeExceeded, StatusCodes.Status400BadRequest);
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(fileExtension))
            {
                throw new AppException(ErrorCodes.ValidationInvalidFileFormat, StatusCodes.Status400BadRequest);
            }

            var webRootPath = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var uploadsFolder = Path.Combine(webRootPath, "uploads", "avatars");
            Directory.CreateDirectory(uploadsFolder);

            // Delete existing avatar files for this user (with any extension)
            if (!string.IsNullOrEmpty(existingAvatarUrl))
            {
                var existingFileName = Path.GetFileName(existingAvatarUrl);
                var existingFilePath = Path.Combine(uploadsFolder, existingFileName);

                if (File.Exists(existingFilePath))
                {
                    File.Delete(existingFilePath);
                }
            }

            // Also check for any files matching the pattern userid_avt.*
            var userAvatarPattern = $"{userId}_avt.*";
            var existingFiles = Directory.GetFiles(uploadsFolder, userAvatarPattern);
            foreach (var existingFile in existingFiles)
            {
                File.Delete(existingFile);
            }

            // Create new filename in format: userid_avt.extension
            var fileName = $"{userId}_avt{fileExtension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Return relative path that works with UseStaticFiles()
            return $"/uploads/avatars/{fileName}";
        }
    }
}
