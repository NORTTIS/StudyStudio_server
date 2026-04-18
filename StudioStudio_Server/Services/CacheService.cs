using Microsoft.Extensions.Caching.Memory;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Centralized caching service with invalidation support
    /// Manages cache keys and expiration policies for all cached data
    /// Provides methods to invalidate cache when data changes (e.g., admin updates)
    /// </summary>
    public class CacheService(IMemoryCache cache, ILogger<CacheService> logger) : ICacheService
    {
        // Default cache durations
        private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan UserProfileExpiration = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan SubscriptionExpiration = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan FreePlanExpiration = TimeSpan.FromHours(1);
        private static readonly TimeSpan AnnouncementExpiration = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan AiRequestCountExpiration = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan UserGroupsExpiration = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Get cached value or set it using factory function if not exists
        /// Pattern: Cache-Aside (Lazy Loading)
        /// </summary>
        public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class
        {
            // Try to get from cache first
            if (cache.TryGetValue(key, out T? cachedValue))
            {
                logger.LogDebug("Cache HIT: {Key}", key);
                return cachedValue;
            }

            // Cache miss - fetch from source
            logger.LogDebug("Cache MISS: {Key}", key);
            var value = await factory();

            await SetAsync(key, value, expiration ?? DefaultExpiration);

            return value;
        }

        /// <summary>
        /// Get value from cache
        /// </summary>
        public Task<T?> GetAsync<T>(string key) where T : class
        {
            cache.TryGetValue(key, out T? value);
            return Task.FromResult(value);
        }

        /// <summary>
        /// Set value in cache with expiration
        /// </summary>
        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? DefaultExpiration
            };

            cache.Set(key, value, cacheOptions);
            logger.LogDebug("Cache SET: {Key} (Expiration: {Expiration})", key, expiration ?? DefaultExpiration);
            
            return Task.CompletedTask;
        }

        /// <summary>
        /// Remove specific cache entry
        /// </summary>
        public Task RemoveAsync(string key)
        {
            cache.Remove(key);
            logger.LogDebug("Cache REMOVE: {Key}", key);
            return Task.CompletedTask;
        }

        // ==================== CACHE KEY GENERATORS ====================
        
        public string GetUserSubscriptionKey(Guid userId) => $"user_subscription:{userId}";
        
        public string GetFreePlanKey() => "free_plan:default";
        
        public string GetUserProfileKey(Guid userId) => $"user_profile:{userId}";
        
        public string GetAnnouncementsKey() => "announcements:active";
        
        public string GetUserAnnouncementsKey(Guid userId) => $"user_announcements:{userId}";
        
        public string GetAiRequestCountKey(Guid userId) => $"ai_request_count:{userId}:{DateTime.UtcNow:yyyy-MM-dd}";
        
        public string GetUserGroupsKey(Guid userId) => $"user_groups:{userId}";

        // AI Tool cache key format: ai:tool:{userId}:{scope}:{toolName}:{paramsHash}
        // scope: group:{groupId} | studio:{studioId} | personal
        public string GetAIToolCacheKey(Guid userId, Guid? groupId, Guid? studioId, string toolName, string paramsHash)
        {
            var scope = groupId.HasValue
                ? $"group:{groupId.Value}"
                : studioId.HasValue
                    ? $"studio:{studioId.Value}"
                    : "personal";

            return $"ai:tool:{userId}:{scope}:{toolName}:{paramsHash}";
        }


        /// <summary>
        /// Get appropriate cache expiration for a given cache key
        /// Returns the predefined expiration time based on key pattern
        /// </summary>
        public TimeSpan GetExpirationForKey(string key)
        {
            if (key.StartsWith("user_profile:"))
                return UserProfileExpiration;
            
            if (key.StartsWith("user_subscription:"))
                return SubscriptionExpiration;
            
            if (key == "free_plan:default")
                return FreePlanExpiration;
            
            if (key == "announcements:active")
                return AnnouncementExpiration;
            
            if (key.StartsWith("user_announcements:"))
                return AnnouncementExpiration;
            
            if (key.StartsWith("ai_request_count:"))
                return AiRequestCountExpiration;
            
            if (key.StartsWith("user_groups:"))
                return UserGroupsExpiration;
            
            return DefaultExpiration;
        }

        // ==================== BULK INVALIDATION METHODS ====================
        
        /// <summary>
        /// Invalidate all cache entries related to a specific user
        /// Call this when user profile, subscription, or groups change
        /// </summary>
        public async Task InvalidateUserCacheAsync(Guid userId)
        {
            await RemoveAsync(GetUserProfileKey(userId));
            await RemoveAsync(GetUserSubscriptionKey(userId));
            await RemoveAsync(GetUserAnnouncementsKey(userId));
            await RemoveAsync(GetUserGroupsKey(userId));
            await RemoveAsync(GetAiRequestCountKey(userId));
            
            logger.LogInformation("Invalidated all cache for user: {UserId}", userId);
        }

        /// <summary>
        /// Invalidate subscription-related caches
        /// Call this when admin updates subscription plans or user purchases subscription
        /// </summary>
        public async Task InvalidateSubscriptionCachesAsync()
        {
            await RemoveAsync(GetFreePlanKey());
            // Note: Individual user subscription caches remain until they expire
            // If immediate invalidation needed, track all user subscription keys
            
            logger.LogInformation("Invalidated subscription plan caches");
        }

        /// <summary>
        /// Invalidate announcement caches
        /// Call this when admin creates, updates, or deletes announcements
        /// </summary>
        public async Task InvalidateAnnouncementCachesAsync()
        {
            await RemoveAsync(GetAnnouncementsKey());
            // User-specific announcement caches remain until expiration
            // If immediate invalidation needed, track all user announcement keys

            logger.LogInformation("Invalidated announcement caches");
        }

        // ==================== AI TOOL CACHE INVALIDATION ====================
        // Note: IMemoryCache doesn't support pattern matching.
        // For AI tool caching, consider using Redis which supports RemoveByPatternAsync.

        public Task InvalidateAIGroupCacheAsync(Guid groupId)
        {
            logger.LogWarning("AI group cache invalidation requires Redis for pattern-based deletion");
            return Task.CompletedTask;
        }

        public Task InvalidateAIStudioCacheAsync(Guid studioId)
        {
            logger.LogWarning("AI studio cache invalidation requires Redis for pattern-based deletion");
            return Task.CompletedTask;
        }

        public Task InvalidateAIDocumentCacheAsync(Guid userId, Guid? groupId, Guid? studioId)
        {
            logger.LogWarning("AI document cache invalidation requires Redis for pattern-based deletion");
            return Task.CompletedTask;
        }

        public Task InvalidateAIDocumentCacheForGroupAsync(Guid groupId, Guid? studioId = null)
        {
            logger.LogWarning("AI group document cache invalidation requires Redis for pattern-based deletion");
            return Task.CompletedTask;
        }

        public Task InvalidateAIMemberCacheAsync(Guid groupId)
        {
            logger.LogWarning("AI member cache invalidation requires Redis for pattern-based deletion");
            return Task.CompletedTask;
        }

    }
}
