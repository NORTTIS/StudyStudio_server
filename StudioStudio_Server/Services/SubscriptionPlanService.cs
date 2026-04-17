using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    public class SubscriptionPlanService(
        ISubscriptionPlanRepository subscriptionPlanRepository,
        IUserRepository userRepository,
        IUserSubscriptionRepository userSubscriptionRepository,
        ICacheService cacheService) : ISubscriptionPlanService
    {
        private readonly ISubscriptionPlanRepository _subscriptionPlanRepository = subscriptionPlanRepository;
        private readonly IUserRepository _userRepository = userRepository;
        private readonly IUserSubscriptionRepository _userSubscriptionRepository = userSubscriptionRepository;
        private readonly ICacheService _cacheService = cacheService;

        public async Task<SubscriptionPlanResponse> GetAllAsync()
        {
            var listPlan = await _subscriptionPlanRepository.GetAllAsync();
            var response = listPlan.Select(p => new SubscriptionPlanItem
            {
                PlanId = p.PlanId,
                PlanName = p.PlanName,
                Price = p.Price,
                BillingCycle = p.BillingCycle,
                Description = p.Description,
                MaxStudios = p.MaxStudios,
                MaxStorageMb = p.MaxStorageMb,
                MaxAiRequestsPerDay = p.MaxAiRequestsPerDay,
                MaxGroups = p.MaxGroups,
                MaxMembersPerGroup = p.MaxMembersPerGroup
            }).ToList();
            return new SubscriptionPlanResponse
            {
                Plans = response,
            };
        }

        /// <summary>
        /// Get subscription statistics including user counts and plan details
        /// Calculates: Total active users, Free users, Premium users, Conversion rate
        /// Returns all plans with subscriber counts
        /// Free users = Total active users - Premium users (no direct subscription record)
        /// Premium users = Users who have ever subscribed to paid plans (BillingCycle > 0)
        /// </summary>
        public async Task<SubscriptionStatisticsResponse> GetStatisticsAsync()
        {
            // Get total active users
            int totalActiveUsers = await _userRepository.CountActiveUsersAsync();

            // Get premium users count (users who have ever subscribed to paid plans)
            int premiumUsers = await _userSubscriptionRepository.CountPremiumUsersAsync();

            // Calculate free users (active users who never subscribed to premium)
            int freeUsers = totalActiveUsers - premiumUsers;

            // Calculate conversion rate
            decimal conversionRate = totalActiveUsers > 0 
                ? Math.Round((decimal)premiumUsers / totalActiveUsers * 100, 2) 
                : 0;

            // Get all plans including inactive
            var allPlans = await _subscriptionPlanRepository.GetAllIncludingInactiveAsync();

            // Get subscriber counts per paid plan (active subscriptions only)
            var subscriberCounts = await _userSubscriptionRepository.GetSubscriberCountsByPlanAsync();

            // Find Free Plan to add free users count
            var freePlan = allPlans.FirstOrDefault(p => p.BillingCycle == BillingCycle.Free);

            // Map plans to response
            var planDetails = allPlans.Select(p => new SubscriptionPlanDetail
            {
                PlanId = p.PlanId,
                PlanName = p.PlanName,
                Price = p.Price,
                BillingCycle = p.BillingCycle,
                Description = p.Description,
                MaxStudios = p.MaxStudios,
                MaxStorageMb = p.MaxStorageMb,
                MaxAiRequestsPerDay = p.MaxAiRequestsPerDay,
                MaxGroups = p.MaxGroups,
                MaxMembersPerGroup = p.MaxMembersPerGroup,
                IsActive = p.IsActive,
                // For Free Plan: use calculated free users count
                // For Paid Plans: use active subscription count
                SubscriberCount = p.BillingCycle == BillingCycle.Free 
                    ? freeUsers 
                    : (subscriberCounts.ContainsKey(p.PlanId) ? subscriberCounts[p.PlanId] : 0)
            }).ToList();

            return new SubscriptionStatisticsResponse
            {
                UserStats = new UserStatistics
                {
                    TotalActiveUsers = totalActiveUsers,
                    FreeUsers = freeUsers,
                    PremiumUsers = premiumUsers,
                    ConversionRate = conversionRate
                },
                Plans = planDetails
            };
        }

        /// <summary>
        /// Update subscription plan information
        /// Only admin can update plans
        /// Validates plan exists before updating
        /// CACHE: Invalidates subscription caches after update
        /// </summary>
        public async Task<SubscriptionPlanDetail> UpdatePlanAsync(UpdateSubscriptionPlanRequest request)
        {
            // Get plan by ID
            var plan = await _subscriptionPlanRepository.GetByIdAsync(request.PlanId);
            
            if (plan == null)
            {
                throw new AppException(ErrorCodes.SubscriptionPlanNotFound, StatusCodes.Status404NotFound);
            }

            // Update plan properties
            plan.PlanName = request.PlanName;
            plan.Price = request.Price;
            plan.BillingCycle = request.BillingCycle;
            plan.Description = request.Description;
            plan.MaxStudios = request.MaxStudios;
            plan.MaxStorageMb = request.MaxStorageMb;
            plan.MaxAiRequestsPerDay = request.MaxAiRequestsPerDay;
            plan.MaxGroups = request.MaxGroups;
            plan.MaxMembersPerGroup = request.MaxMembersPerGroup;
            plan.IsActive = request.IsActive;

            // Save changes
            await _subscriptionPlanRepository.UpdateAsync(plan);

            // ✅ INVALIDATE SUBSCRIPTION CACHES - Admin updated subscription plans
            await _cacheService.InvalidateSubscriptionCachesAsync();

            // Calculate subscriber count based on plan type
            int subscriberCount;
            if (plan.BillingCycle == BillingCycle.Free)
            {
                // For Free Plan: calculate free users
                int totalActiveUsers = await _userRepository.CountActiveUsersAsync();
                int premiumUsers = await _userSubscriptionRepository.CountPremiumUsersAsync();
                subscriberCount = totalActiveUsers - premiumUsers;
            }
            else
            {
                // For Paid Plans: get active subscription count
                var subscriberCounts = await _userSubscriptionRepository.GetSubscriberCountsByPlanAsync();
                subscriberCount = subscriberCounts.ContainsKey(plan.PlanId) ? subscriberCounts[plan.PlanId] : 0;
            }

            // Return updated plan details
            return new SubscriptionPlanDetail
            {
                PlanId = plan.PlanId,
                PlanName = plan.PlanName,
                Price = plan.Price,
                BillingCycle = plan.BillingCycle,
                Description = plan.Description,
                MaxStudios = plan.MaxStudios,
                MaxStorageMb = plan.MaxStorageMb,
                MaxAiRequestsPerDay = plan.MaxAiRequestsPerDay,
                MaxGroups = plan.MaxGroups,
                MaxMembersPerGroup = plan.MaxMembersPerGroup,
                IsActive = plan.IsActive,
                SubscriberCount = subscriberCount
            };
        }
    }
}
