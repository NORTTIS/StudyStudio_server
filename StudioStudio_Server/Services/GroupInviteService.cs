using StackExchange.Redis;
using StudioStudio_Server.Models;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StudioStudio_Server.Services
{
    public class GroupInviteService : IGroupInviteService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<GroupInviteService> _logger;
        private const int TOKEN_LIFETIME_MINUTES = 15;
        private const int RATE_LIMIT_WINDOW_MINUTES = 15;
        private const int MAX_LINKS_PER_WINDOW = 5;

        public GroupInviteService(
            IConnectionMultiplexer redis,
            ILogger<GroupInviteService> logger)
        {
            _redis = redis;
            _logger = logger;
        }

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

        public async Task<bool> StoreInviteTokenAsync(string token, GroupInviteToken inviteData)
        {
            IDatabase db = _redis.GetDatabase();
            string key = $"group:invite:{token}";

            string jsonData = JsonSerializer.Serialize(inviteData);

            TimeSpan expiry = TimeSpan.FromMinutes(TOKEN_LIFETIME_MINUTES);

            bool result = await db.StringSetAsync(key, jsonData, expiry);

            if (result)
            {
                _logger.LogInformation("Invite token stored for group {GroupId} with role {Role}. Expires in {Minutes} minutes.",
                    inviteData.GroupId, inviteData.Role, TOKEN_LIFETIME_MINUTES);
            }

            return result;
        }

        public async Task<GroupInviteToken?> GetInviteTokenDataAsync(string token)
        {
            IDatabase db = _redis.GetDatabase();
            string key = $"group:invite:{token}";

            RedisValue value = await db.StringGetAsync(key);

            if (value.IsNullOrEmpty)
            {
                _logger.LogWarning("Invite token not found or expired: {Token}", token);
                return null;
            }

            GroupInviteToken? inviteData = JsonSerializer.Deserialize<GroupInviteToken>(value.ToString());

            return inviteData;
        }

        public async Task<bool> CheckInviteCreationRateLimitAsync(Guid groupId, Guid userId)
        {
            IDatabase db = _redis.GetDatabase();
            string key = $"invite:ratelimit:{groupId}:{userId}";

            RedisValue value = await db.StringGetAsync(key);

            int currentCount = 0;
            if (!value.IsNullOrEmpty)
            {
                int.TryParse(value.ToString(), out currentCount);
            }

            if (currentCount >= MAX_LINKS_PER_WINDOW)
            {
                _logger.LogWarning("Rate limit exceeded for user {UserId} in group {GroupId}. Count: {Count}",
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
