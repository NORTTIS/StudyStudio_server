using Microsoft.AspNetCore.Http;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    public class AdminUserService : IAdminUserService
    {
        private readonly IUserRepository _userRepository;

        public AdminUserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        /// <summary>
        /// Get paginated list of users with filters
        /// </summary>
        public async Task<UserListResponse> GetUsersAsync(GetUsersRequest request)
        {
            // Get paginated users
            var (users, totalCount) = await _userRepository.GetUsersAsync(
                request.SearchTerm,
                request.Status,
                request.Package,
                request.PageNumber,
                request.PageSize);

            // Get studio counts for all users in the list (to avoid N+1)
            var userIds = users.Select(u => u.UserId).ToList();
            var studioCounts = await _userRepository.GetStudioCountsAsync(userIds);

            // Get summary statistics
            var summary = await _userRepository.GetUserSummaryAsync();

            // Map to response DTOs
            var userListItems = users.Select(u => MapToUserListItem(u, studioCounts)).ToList();

            return new UserListResponse
            {
                Summary = new UserListSummary
                {
                    TotalUsers = summary.TotalUsers,
                    ActiveUsers = summary.ActiveUsers,
                    InactiveUsers = summary.InactiveUsers,
                    DeletedUsers = summary.DeletedUsers,
                    PremiumUsers = summary.PremiumUsers,
                    FreeUsers = summary.FreeUsers
                },
                UserList = userListItems,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                TotalCount = totalCount
            };
        }

        /// <summary>
        /// Get detailed user information by ID
        /// </summary>
        public async Task<UserDetailItem> GetUserDetailAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdWithDetailsAsync(userId);

            if (user == null)
            {
                throw new KeyNotFoundException($"User with ID {userId} not found");
            }

            // Get studio count for this user
            var studioCount = await _userRepository.GetStudioCountsAsync(new List<Guid> { userId });

            return MapToUserDetailItem(user, studioCount);
        }

        private UserListItem MapToUserListItem(User user, Dictionary<Guid, int> studioCounts)
        {
            // Get active subscription
            var activeSubscription = user.UserSubscriptions
                .FirstOrDefault(us => us.Plan.BillingCycle > 0 && us.IsActive && us.EndDate > DateTime.UtcNow);

            // Determine package
            var package = activeSubscription != null ? "Premium" : "Free";

            // Get last login from refresh token
            var lastLoginAt = user.RefreshTokens
                .Where(rt => !rt.IsRevoked && rt.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(rt => rt.ExpiresAt)
                .Select(rt => (DateTime?)rt.ExpiresAt.AddHours(-7)) // Approximate login time from expiry
                .FirstOrDefault();

            // Map status to Vietnamese
            var statusText = user.Status switch
            {
                UserStatus.Active => "Hoạt động",
                UserStatus.Inactive => "Bị vô hiệu",
                UserStatus.Deleted => "Đã xóa",
                _ => "Không xác định"
            };

            return new UserListItem
            {
                UserId = user.UserId,
                Email = user.Email,
                FullName = $"{user.FirstName} {user.LastName}".Trim(),
                Package = package,
                GroupCount = user.GroupParticipants?.Count ?? 0,
                StudioCount = studioCounts.GetValueOrDefault(user.UserId, 0),
                CreatedAt = user.CreatedAt,
                LastLoginAt = lastLoginAt,
                Status = statusText
            };
        }

        private UserDetailItem MapToUserDetailItem(User user, Dictionary<Guid, int> studioCounts)
        {
            var baseItem = MapToUserListItem(user, studioCounts);

            // Get active subscription for detailed info
            var activeSubscription = user.UserSubscriptions
                .FirstOrDefault(us => us.Plan.BillingCycle > 0 && us.IsActive && us.EndDate > DateTime.UtcNow);

            SubscriptionPlanInfo? subscriptionInfo = null;
            if (activeSubscription != null)
            {
                subscriptionInfo = new SubscriptionPlanInfo
                {
                    PlanId = activeSubscription.Plan.PlanId,
                    PlanName = activeSubscription.Plan.PlanName,
                    Price = activeSubscription.Plan.Price,
                    BillingCycle = activeSubscription.Plan.BillingCycle == BillingCycle.Monthly ? "Monthly" : "Free",
                    StartDate = activeSubscription.StartDate,
                    EndDate = activeSubscription.EndDate,
                    IsActive = activeSubscription.IsActive
                };
            }

            return new UserDetailItem
            {
                UserId = baseItem.UserId,
                Email = baseItem.Email,
                FullName = baseItem.FullName,
                Package = baseItem.Package,
                GroupCount = baseItem.GroupCount,
                StudioCount = baseItem.StudioCount,
                CreatedAt = baseItem.CreatedAt,
                LastLoginAt = baseItem.LastLoginAt,
                Status = baseItem.Status,
                PhoneNumber = user.PhoneNumber,
                Bio = user.Bio,
                AvatarUrl = user.AvatarUrl,
                IsVerify = user.IsVerify,
                UpdatedAt = user.UpdatedAt,
                IsAdmin = user.IsAdmin,
                Subscription = subscriptionInfo
            };
        }

        /// <summary>
        /// Update user status (activate/inactivate)
        /// </summary>
        public async Task UpdateUserStatusAsync(Guid userId, UserStatus status)
        {
            // Validate status - only allow Active or Inactive (not Deleted for this operation)
            if (status == UserStatus.Deleted)
            {
                throw new AppException(
                    ErrorCodes.UserInvalidStatus,
                    StatusCodes.Status400BadRequest);
            }

            // Check if user exists
            var user = await _userRepository.GetByIdIncludingDeletedAsync(userId);
            if (user == null)
            {
                throw new AppException(
                    ErrorCodes.UserNotFound,
                    StatusCodes.Status404NotFound);
            }

            // Prevent modifying admin users
            if (user.IsAdmin)
            {
                throw new AppException(
                    ErrorCodes.UserCannotModifyAdmin,
                    StatusCodes.Status403Forbidden);
            }

            await _userRepository.UpdateUserStatusAsync(userId, status);
        }
    }
}
