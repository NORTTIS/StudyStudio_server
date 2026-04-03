using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using StudioStudio_Server.Exceptions;
using System.Text.RegularExpressions;
using StudioStudio_Server.Models.DTOs.Response;
using EnumsNET;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service handling user profile and account management
    /// Manages: Profile updates, password changes, account deletion, avatar uploads
    /// Also provides AI request usage information for rate limiting display
    /// OPTIMIZED: Uses caching and automatic cache invalidation
    /// </summary>
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IWebHostEnvironment _environment;
        private readonly IUserSubscriptionRepository _userSubscriptionRepository;
        private readonly IAIRequestLogRepository _aiRequestLogRepository;
        private readonly ICacheService _cacheService;

        // Password must be 10-20 characters long, contain at least one uppercase letter, one lowercase letter, and one digit
        private readonly Regex PasswordRegex = new(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)[A-Za-z\d@$!%*?&]{10,20}$", RegexOptions.Compiled);

        public UserService(
            IUserRepository userRepository,
            IPasswordHasher<User> passwordHasher,
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            IWebHostEnvironment environment,
            IUserSubscriptionRepository userSubscriptionRepository,
            IAIRequestLogRepository aiRequestLogRepository,
            ICacheService cacheService)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _environment = environment;
            _userSubscriptionRepository = userSubscriptionRepository;
            _aiRequestLogRepository = aiRequestLogRepository;
            _cacheService = cacheService;
        }

        /// <summary>
        /// Get user by ID
        /// Returns: User entity or null if not found
        /// CACHED: Uses UserProfileExpiration (15 minutes)
        /// </summary>
        public async Task<User?> GetByIdAsync(Guid userId)
        {
            var cacheKey = _cacheService.GetUserProfileKey(userId);
            
            return await _cacheService.GetOrSetAsync(
                cacheKey,
                async () => await _userRepository.GetByIdAsync(userId),
                _cacheService.GetExpirationForKey(cacheKey)
            );
        }

        /// <summary>
        /// Get user by email
        /// Returns: User entity or null if not found
        /// </summary>
        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _userRepository.GetByEmailAsync(email);
        }

        /// <summary>
        /// Update user information
        /// Auto-sets UpdatedAt = UtcNow
        /// CACHE: Invalidates user cache after update
        /// </summary>
        public async Task UpdateAsync(User user)
        {
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);
            
            // Invalidate user cache
            await _cacheService.InvalidateUserCacheAsync(user.UserId);
        }

        /// <summary>
        /// Delete user account (ghost user)
        /// Validate: User exists and not already deleted
        /// Action:
        ///   - Set Status = UserStatus.Deleted
        ///   - Anonymize user data (ghostUser): clear email, name, password, etc.
        ///   - Email is replaced with unique placeholder to allow new registration
        /// Note: User data remains in database for referential integrity
        /// CACHE: Invalidates user cache after deletion
        /// </summary>
        public async Task DeleteAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdIncludingDeletedAsync(userId);
            if (user == null)
            {
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
            }

            if (user.Status == UserStatus.Deleted)
            {
                throw new AppException(ErrorCodes.UserAccountAlreadyDeleted, StatusCodes.Status400BadRequest);
            }

            // Apply ghostUser: anonymize user data
            // Note: Email is kept as-is because the unique index only applies to non-deleted users
            // This allows the original email to be reused after the user is restored (if needed)
            user.Status = UserStatus.Deleted;
            user.FirstName = "Deleted";
            user.LastName = "User";
            // Generate random password hash to satisfy NOT NULL constraint
            // This password is unusable since user cannot login (Status = Deleted)
            user.PasswordHash = _passwordHasher.HashPassword(user, Guid.NewGuid().ToString("N"));
            user.PhoneNumber = null;
            user.Bio = null;
            user.AvatarUrl = null;
            user.GoogleId = null;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepository.UpdateAsync(user);

            // Invalidate all user-related cache
            await _cacheService.InvalidateUserCacheAsync(userId);
        }

        /// <summary>
        /// Change user password
        /// Validate:
        /// - Current password is correct
        /// - New password meets strength requirements
        /// - New password matches confirmation
        /// - New password is different from current password
        /// CACHE: Invalidates user cache after password change
        /// </summary>
        public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
        {
            // Get user
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
            }

            // Verify current password
            var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash!, request.CurrentPassword);
            if (verifyResult != PasswordVerificationResult.Success)
            {
                throw new AppException(ErrorCodes.AuthIncorrectCurrentPassword, StatusCodes.Status400BadRequest);
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
            
            // Invalidate user cache
            await _cacheService.InvalidateUserCacheAsync(userId);
        }

        /// <summary>
        /// Update user profile information
        /// Updates: FirstName, LastName, PhoneNumber, Bio, Language, EmailNotificationEnabled, Avatar
        /// Validation:
        /// - Avatar: Max 5MB, only .jpg/.jpeg/.png allowed
        /// - Old avatar file is automatically deleted when new one is uploaded
        /// Avatar storage: wwwroot/uploads/avatars/{userId}_avt.{ext}
        /// CACHE: Invalidates user cache after profile update
        /// </summary>
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
            
            // Invalidate all user-related cache after profile update
            await _cacheService.InvalidateUserCacheAsync(userId);
        }

        /// <summary>
        /// Validate password strength
        /// Requirements: 10-20 chars, at least one uppercase, one lowercase, one digit
        /// </summary>
        private bool IsValidPassword(string password)
        {
            return PasswordRegex.IsMatch(password);
        }

        /// <summary>
        /// Save user avatar to local file system
        /// Validation:
        /// - File size max 5MB
        /// - Only .jpg, .jpeg, .png allowed
        /// Storage:
        /// - Path: wwwroot/uploads/avatars/
        /// - Filename: {userId}_avt.{ext}
        /// - Deletes old avatar files before saving new one
        /// Returns: Relative URL path to avatar (/uploads/avatars/{filename})
        /// </summary>
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

        /// <summary>
        /// Get AI request usage information for user
        /// Returns: (usedToday, dailyLimit) tuple
        /// Used to display AI usage quota in frontend
        /// Daily limit depends on subscription plan (Free: 20, Premium: 100)
        /// CACHED: Subscription plan uses SubscriptionExpiration (5 minutes)
        /// </summary>
        public async Task<(int usedToday, int dailyLimit)> GetAiRequestLimitInfoAsync(Guid userId)
        {
            // Get today's start time
            DateTime startOfDay = DateTime.UtcNow.Date;

            // Always query AI request count fresh for accurate rate limiting
            // (int cannot be cached with current ICacheService constraint)
            int usedToday = await _aiRequestLogRepository.CountTodayRequestsAsync(userId, startOfDay);

            // Cache subscription plan with proper expiration
            var subscriptionKey = _cacheService.GetUserSubscriptionKey(userId);
            SubscriptionPlan? subscriptionPlan = await _cacheService.GetOrSetAsync(
                subscriptionKey,
                async () => await _userSubscriptionRepository.GetSubscriptionPlanByUserIdAsync(userId),
                _cacheService.GetExpirationForKey(subscriptionKey)
            );

            int dailyLimit = subscriptionPlan?.MaxAiRequestsPerDay ?? 20; // Default: Free Plan = 20
            Console.WriteLine($"User {userId} has used {usedToday}/{dailyLimit} AI requests today.");
            Console.WriteLine($"Subscription Plan: {subscriptionPlan?.PlanName ?? "Free Plan"}, Max AI Requests/Day: {dailyLimit}");

            return (usedToday, dailyLimit);
        }

        public async Task<SubscriptionPlanItem> GetUserSubscriptionPlan(Guid userId)
        {
            // Cache subscription plan with proper expiration
            var subscriptionKey = _cacheService.GetUserSubscriptionKey(userId);
            SubscriptionPlan? subscriptionPlan = await _cacheService.GetOrSetAsync(
                subscriptionKey,
                async () => await _userSubscriptionRepository.GetSubscriptionPlanByUserIdAsync(userId),
                _cacheService.GetExpirationForKey(subscriptionKey)
            );

            return new SubscriptionPlanItem
            {
                PlanId = subscriptionPlan!.PlanId,
                PlanName = subscriptionPlan!.PlanName,
                Price = subscriptionPlan.Price,
                BillingCycle = subscriptionPlan.BillingCycle,
                Description = subscriptionPlan.Description,
                MaxStudios = subscriptionPlan.MaxStudios,
                MaxStorageMb = subscriptionPlan.MaxStorageMb,
                MaxAiRequestsPerDay = subscriptionPlan.MaxAiRequestsPerDay,
                MaxGroups = subscriptionPlan.MaxGroups,
                MaxMembersPerGroup = subscriptionPlan.MaxMembersPerGroup
            };
        }
    }
}
