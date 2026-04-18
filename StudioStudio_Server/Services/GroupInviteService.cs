using StackExchange.Redis;
using StudioStudio_Server.Models.Caches;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Cryptography;
using System.Text.Json;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service handling group invitation token management using Redis cache
    /// Manages temporary invite links with expiration and rate limiting
    /// Token lifetime: 15 minutes
    /// Rate limit: 5 invite links per 15 minutes per user per group
    /// </summary>
    public class GroupInviteService(
        IConnectionMultiplexer redis,
        ILogger<GroupInviteService> logger) : IGroupInviteService
    {
        private const int TOKEN_LIFETIME_MINUTES = 15;
        private const int RATE_LIMIT_WINDOW_MINUTES = 15;
        private const int MAX_LINKS_PER_WINDOW = 5;

        /// <summary>
        /// Generate cryptographically secure random invite token
        /// Returns: URL-safe base64 string (32 random bytes)
        /// Format: Replaces + with -, / with _, removes padding =
        /// </summary>
        public async Task<string> GenerateInviteTokenAsync()
        {
            byte[] randomBytes = new byte[32];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }

            string token = Convert.ToBase64String(randomBytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');

            return await Task.FromResult(token);
        }

        /// <summary>
        /// Store invite token data in Redis
        /// Key: "group:invite:{token}"
        /// Value: JSON serialized GroupInviteToken
        /// TTL: 15 minutes
        /// Returns: true if stored successfully
        /// </summary>
        public async Task<bool> StoreInviteTokenAsync(string token, GroupInviteToken inviteData)
        {
            IDatabase db = redis.GetDatabase();
            string key = $"group:invite:{token}";

            string jsonData = JsonSerializer.Serialize(inviteData);

            TimeSpan expiry = TimeSpan.FromMinutes(TOKEN_LIFETIME_MINUTES);

            bool result = await db.StringSetAsync(key, jsonData, expiry);

            if (result)
            {
                logger.LogInformation("Invite token stored for group {GroupId} with role {Role}. Expires in {Minutes} minutes.",
                    inviteData.GroupId, inviteData.Role, TOKEN_LIFETIME_MINUTES);
            }

            return result;
        }

        /// <summary>
        /// Retrieve invite token data from Redis
        /// Returns: GroupInviteToken if found and not expired, null otherwise
        /// Use case: Validate invite link when user clicks on it
        /// </summary>
        public async Task<GroupInviteToken?> GetInviteTokenDataAsync(string token)
        {
            IDatabase db = redis.GetDatabase();
            string key = $"group:invite:{token}";

            RedisValue value = await db.StringGetAsync(key);

            if (value.IsNullOrEmpty)
            {
                logger.LogWarning("Invite token not found or expired: {Token}", token);
                return null;
            }

            GroupInviteToken? inviteData = JsonSerializer.Deserialize<GroupInviteToken>(value.ToString());

            return inviteData;
        }

        /// <summary>
        /// Check rate limit for invite link creation
        /// Key: "invite:ratelimit:{groupId}:{userId}"
        /// Limit: 5 invites per 15 minutes per user per group
        /// Returns: true if user can create invite link, false if rate limit exceeded
        /// Auto-increments counter and sets expiry on first request
        /// </summary>
        public async Task<bool> CheckInviteCreationRateLimitAsync(Guid groupId, Guid userId)
        {
            IDatabase db = redis.GetDatabase();
            string key = $"invite:ratelimit:{groupId}:{userId}";

            RedisValue value = await db.StringGetAsync(key);

            int currentCount = 0;
            if (!value.IsNullOrEmpty)
            {
                int.TryParse(value.ToString(), out currentCount);
            }

            if (currentCount >= MAX_LINKS_PER_WINDOW)
            {
                logger.LogWarning("Rate limit exceeded for user {UserId} in group {GroupId}. Count: {Count}",
                    userId, groupId, currentCount);
                return false;
            }

            long newCount = await db.StringIncrementAsync(key);

            if (newCount == 1)
            {
                await db.KeyExpireAsync(key, TimeSpan.FromMinutes(RATE_LIMIT_WINDOW_MINUTES));
            }

            return true;
        }
    }
}
