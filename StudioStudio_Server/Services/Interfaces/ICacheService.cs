namespace StudioStudio_Server.Services.Interfaces
{
    /// <summary>
    /// Centralized caching service interface
    /// Manages cache keys, expiration, and invalidation for all cached data
    /// </summary>
    public interface ICacheService
    {
        // Core cache operations
        Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null) where T : class;
        Task<T?> GetAsync<T>(string key) where T : class;
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
        Task RemoveAsync(string key);
        Task RemoveByPatternAsync(string pattern);
        
        // Cache key generators
        string GetUserSubscriptionKey(Guid userId);
        string GetFreePlanKey();
        string GetUserProfileKey(Guid userId);
        string GetAnnouncementsKey();
        string GetUserAnnouncementsKey(Guid userId);
        string GetAiRequestCountKey(Guid userId);
        string GetUserGroupsKey(Guid userId);
        
        // Get expiration for key
        TimeSpan GetExpirationForKey(string key);
        
        // Bulk invalidation
        Task InvalidateUserCacheAsync(Guid userId);
        Task InvalidateSubscriptionCachesAsync();
        Task InvalidateAnnouncementCachesAsync();
    }
}
