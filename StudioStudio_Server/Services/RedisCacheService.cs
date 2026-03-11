using StackExchange.Redis;
using StudioStudio_Server.Services.Interfaces;
using System.Text.Json;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Redis-based distributed caching service using StackExchange.Redis directly
    /// Provides cache functionality using Redis for production environments
    /// Benefits:
    /// - Shared cache across multiple servers
    /// - Survives application restarts
    /// - Pattern-based key invalidation (SCAN command)
    /// - Atomic batch operations
    /// - Better performance than IDistributedCache
    /// </summary>
    public class RedisCacheService : ICacheService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _database;
        private readonly ILogger<RedisCacheService> _logger;
        
        // Instance name for all cache keys (same as existing Redis usage)
        private const string INSTANCE_PREFIX = "StudyStudio:Cache:";
        
        // Default cache durations (same as MemoryCacheService)
        private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan UserProfileExpiration = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan SubscriptionExpiration = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan FreePlanExpiration = TimeSpan.FromHours(1);
        private static readonly TimeSpan AnnouncementExpiration = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan AiRequestCountExpiration = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan UserGroupsExpiration = TimeSpan.FromMinutes(10);

        public RedisCacheService(
            IConnectionMultiplexer redis,
            ILogger<RedisCacheService> logger)
        {
            _redis = redis;
            _database = redis.GetDatabase();
            _logger = logger;
        }

        /// <summary>
        /// Get cached value or set it using factory function if not exists
        /// Pattern: Cache-Aside (Lazy Loading)
        /// Uses JSON serialization for Redis storage
        /// </summary>
        public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class
        {
            var redisKey = INSTANCE_PREFIX + key;
            
            try
            {
                // Try to get from cache first
                var cachedValue = await _database.StringGetAsync(redisKey);
                
                if (cachedValue.HasValue)
                {
                    var deserialized = JsonSerializer.Deserialize<T>(cachedValue!);
                    if (deserialized != null)
                    {
                        _logger.LogDebug("Redis Cache HIT: {Key}", key);
                        return deserialized;
                    }
                }

                // Cache miss - fetch from source
                _logger.LogDebug("Redis Cache MISS: {Key}", key);
                var value = await factory();
                
                if (value != null)
                {
                    await SetAsync(key, value, expiration ?? DefaultExpiration);
                }

                return value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis cache error for key {Key}. Falling back to direct query.", key);
                // Fallback: If Redis fails, fetch directly from source
                return await factory();
            }
        }

        /// <summary>
        /// Get value from cache
        /// </summary>
        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            var redisKey = INSTANCE_PREFIX + key;
            
            try
            {
                var cachedValue = await _database.StringGetAsync(redisKey);
                
                if (cachedValue.HasValue)
                {
                    return JsonSerializer.Deserialize<T>(cachedValue!);
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis get error for key {Key}", key);
                return null;
            }
        }

        /// <summary>
        /// Set value in cache with expiration
        /// Serializes object to JSON before storing in Redis
        /// </summary>
        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            var redisKey = INSTANCE_PREFIX + key;
            
            try
            {
                var json = JsonSerializer.Serialize(value);
                await _database.StringSetAsync(redisKey, json, expiration ?? DefaultExpiration);
                
                _logger.LogDebug("Redis Cache SET: {Key} (Expiration: {Expiration})", key, expiration ?? DefaultExpiration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis set error for key {Key}", key);
            }
        }

        /// <summary>
        /// Remove specific cache entry
        /// </summary>
        public async Task RemoveAsync(string key)
        {
            var redisKey = INSTANCE_PREFIX + key;
            
            try
            {
                await _database.KeyDeleteAsync(redisKey);
                _logger.LogDebug("Redis Cache REMOVE: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis remove error for key {Key}", key);
            }
        }

        /// <summary>
        /// Remove all cache entries matching pattern using Redis SCAN command
        /// Example: RemoveByPatternAsync("user_*") removes all keys starting with "user_"
        /// Uses SCAN for production-safe iteration (non-blocking)
        /// </summary>
        public async Task RemoveByPatternAsync(string pattern)
        {
            try
            {
                var redisPattern = INSTANCE_PREFIX + pattern;
                var endpoint = _redis.GetEndPoints().FirstOrDefault();
                
                if (endpoint == null)
                {
                    _logger.LogWarning("No Redis endpoint found for pattern deletion: {Pattern}", pattern);
                    return;
                }

                var server = _redis.GetServer(endpoint);
                var keys = server.Keys(pattern: redisPattern, pageSize: 1000);
                
                var batch = _database.CreateBatch();
                var deleteTasks = new List<Task<bool>>();
                
                foreach (var key in keys)
                {
                    deleteTasks.Add(batch.KeyDeleteAsync(key));
                }
                
                batch.Execute();
                await Task.WhenAll(deleteTasks);
                
                var deletedCount = deleteTasks.Count(t => t.Result);
                _logger.LogInformation("Redis Cache PATTERN DELETE: {Pattern}, Deleted {Count} keys", pattern, deletedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis pattern delete error for pattern {Pattern}", pattern);
            }
        }

        // ==================== CACHE KEY GENERATORS ====================
        
        public string GetUserSubscriptionKey(Guid userId) => $"user_subscription:{userId}";
        
        public string GetFreePlanKey() => "free_plan:default";
        
        public string GetUserProfileKey(Guid userId) => $"user_profile:{userId}";
        
        public string GetAnnouncementsKey() => "announcements:active";
        
        public string GetUserAnnouncementsKey(Guid userId) => $"user_announcements:{userId}";
        
        public string GetAiRequestCountKey(Guid userId) => $"ai_request_count:{userId}:{DateTime.UtcNow:yyyy-MM-dd}";
        
        public string GetUserGroupsKey(Guid userId) => $"user_groups:{userId}";

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
        /// Uses batch operations for better performance
        /// </summary>
        public async Task InvalidateUserCacheAsync(Guid userId)
        {
            try
            {
                var batch = _database.CreateBatch();
                
                var tasks = new[]
                {
                    batch.KeyDeleteAsync(INSTANCE_PREFIX + GetUserProfileKey(userId)),
                    batch.KeyDeleteAsync(INSTANCE_PREFIX + GetUserSubscriptionKey(userId)),
                    batch.KeyDeleteAsync(INSTANCE_PREFIX + GetUserAnnouncementsKey(userId)),
                    batch.KeyDeleteAsync(INSTANCE_PREFIX + GetUserGroupsKey(userId)),
                    batch.KeyDeleteAsync(INSTANCE_PREFIX + GetAiRequestCountKey(userId))
                };
                
                batch.Execute();
                await Task.WhenAll(tasks);
                
                var deletedCount = tasks.Count(t => t.Result);
                _logger.LogInformation("Redis: Invalidated {Count}/5 cache entries for user: {UserId}", deletedCount, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis error invalidating user cache for {UserId}", userId);
            }
        }

        /// <summary>
        /// Invalidate subscription-related caches
        /// Call this when admin updates subscription plans
        /// For user subscription changes, use InvalidateUserCacheAsync instead
        /// </summary>
        public async Task InvalidateSubscriptionCachesAsync()
        {
            try
            {
                await RemoveAsync(GetFreePlanKey());
                // Could also invalidate all user_subscription:* keys if needed:
                // await RemoveByPatternAsync("user_subscription:*");
                
                _logger.LogInformation("Redis: Invalidated subscription plan caches");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis error invalidating subscription caches");
            }
        }

        /// <summary>
        /// Invalidate announcement caches
        /// Call this when admin creates, updates, or deletes announcements
        /// </summary>
        public async Task InvalidateAnnouncementCachesAsync()
        {
            try
            {
                await RemoveAsync(GetAnnouncementsKey());
                // Could also invalidate all user_announcements:* keys if needed:
                // await RemoveByPatternAsync("user_announcements:*");
                
                _logger.LogInformation("Redis: Invalidated announcement caches");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis error invalidating announcement caches");
            }
        }
    }
}
