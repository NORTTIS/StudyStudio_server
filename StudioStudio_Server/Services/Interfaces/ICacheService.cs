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

        // AI Tool cache keys
        string GetAIToolCacheKey(Guid userId, Guid? groupId, Guid? studioId, string toolName, string paramsHash);
        string GetAIGroupToolPattern(Guid groupId);
        string GetAIUserToolPattern(Guid userId);
        string GetAIStudioToolPattern(Guid studioId);

        // Get expiration for key
        TimeSpan GetExpirationForKey(string key);

        // Bulk invalidation
        Task InvalidateUserCacheAsync(Guid userId);
        Task InvalidateSubscriptionCachesAsync();
        Task InvalidateAnnouncementCachesAsync();

        // AI Tool cache invalidation - gọi khi data thay đổi
        Task InvalidateAITaskCacheAsync(Guid userId, Guid? groupId);
        Task InvalidateAIGroupCacheAsync(Guid groupId);
        Task InvalidateAIStudioCacheAsync(Guid studioId);
        Task InvalidateAIDocumentCacheAsync(Guid userId, Guid? groupId, Guid? studioId);
        Task InvalidateAIDocumentCacheForGroupAsync(Guid groupId, Guid? studioId = null);
        Task InvalidateAIMemberCacheAsync(Guid groupId);
        Task InvalidateAIUserCacheAsync(Guid userId);
    }
}
