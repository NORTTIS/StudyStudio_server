using StudioStudio_Server.Services.AI.Interfaces;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace StudioStudio_Server.Services.AI
{
    /// <summary>
    /// Decorator service that adds Redis caching to AI tool execution.
    /// Automatically caches tool results with a short TTL only.
    /// Freshness is handled by expiration (30s), not by active invalidation in this class.
    /// </summary>
    public class AIToolCacheService(ICacheService cacheService, ILogger<AIToolCacheService> logger)
    {
        private readonly ICacheService _cacheService = cacheService;
        private readonly ILogger<AIToolCacheService> _logger = logger;

        // Short TTL for AI tool cache - balances performance with data freshness
        private static readonly TimeSpan ToolCacheTtl = TimeSpan.FromSeconds(30);

        // Tools that should not be cached (too dynamic or paginated)
        private static readonly HashSet<string> NoCacheTools = new(StringComparer.OrdinalIgnoreCase)
        {
            "get_tasks", "get_personal_tasks", "get_deadlines", "get_personal_deadlines"
        };


        /// <summary>
        /// Execute a tool with caching. Results are cached for short duration.
        /// Cache key includes userId, groupId, toolName, and hashed parameters.
        /// </summary>
        public async Task<AIQueryResult> ExecuteWithCacheAsync(
            IAITool tool,
            Guid userId,
            Guid? groupId,
            JsonObject parameters,
            AIQueryContext context,
            CancellationToken cancellationToken)
        {
            var toolName = tool.Name;

            // Task tools are not cached - data changes too frequently with pagination
            if (NoCacheTools.Contains(toolName))
            {
                return await tool.ExecuteAsync(context, parameters, cancellationToken);
            }

            var paramsHash = ComputeParamsHash(parameters);
            var cacheKey = _cacheService.GetAIToolCacheKey(userId, groupId, context.StudioId, toolName, paramsHash);

            _logger.LogDebug("[AI-CACHE] Execute tool {Tool} with cache. Key: {Key}", toolName, cacheKey);

            // Try cache first
            try
            {
                var cached = await _cacheService.GetAsync<AIQueryResult>(cacheKey);
                if (cached != null)
                {
                    _logger.LogInformation("[AI-CACHE] HIT: {Tool} for user {UserId}, group {GroupId}",
                        toolName, userId, groupId);
                    return cached;
                }

                _logger.LogInformation("[AI-CACHE] MISS: {Tool} for user {UserId}, group {GroupId}. Executing...",
                    toolName, userId, groupId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AI-CACHE] Error reading cache for tool {Tool}. Executing without cache hit", toolName);
            }

            // Execute tool exactly once
            var result = await tool.ExecuteAsync(context, parameters, cancellationToken);

            // Cache successful results only. Cache write failures must not re-execute the tool.
            if (result.IsSuccess)
            {
                try
                {
                    await _cacheService.SetAsync(cacheKey, result, ToolCacheTtl);
                    _logger.LogInformation("[AI-CACHE] Stored result for {Tool}. TTL: {TTL}s",
                        toolName, ToolCacheTtl.TotalSeconds);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[AI-CACHE] Failed to write cache for tool {Tool}. Returning original result", toolName);
                }
            }

            return result;
        }

        /// <summary>
        /// Compute a hash of the tool parameters for cache key uniqueness.
        /// </summary>
        private string ComputeParamsHash(JsonObject parameters)
        {
            var json = parameters.ToJsonString();
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
            return Convert.ToHexString(bytes)[..16];
        }
    }
}
