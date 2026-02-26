using StackExchange.Redis;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Services.Interfaces;
using System.Text.Json;

namespace StudioStudio_Server.Services
{
    public class EmailVerificationCacheService : IEmailVerificationCacheService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _database;
        private readonly ILogger<EmailVerificationCacheService> _logger;

        // Redis key prefixes
        private const string TOKEN_BY_EMAIL_PREFIX = "email_verification:token_by_email:";
        private const string TOKEN_DATA_PREFIX = "email_verification:token_data:";
        private const string RATE_LIMIT_PREFIX = "email_verification:rate_limit:";

        // Rate limit settings
        private const int MAX_REQUESTS_PER_WINDOW = 5;
        private static readonly TimeSpan RATE_LIMIT_WINDOW = TimeSpan.FromMinutes(15);

        public EmailVerificationCacheService(
            IConnectionMultiplexer redis,
            ILogger<EmailVerificationCacheService> logger)
        {
            _redis = redis;
            _database = redis.GetDatabase();
            _logger = logger;
        }

        public async Task<EmailVerificationDataRedis?> GetVerificationDataByTokenAsync(string token)
        {
            try
            {
                var tokenKey = TOKEN_DATA_PREFIX + token;
                var json = await _database.StringGetAsync(tokenKey);

                if (!json.HasValue)
                {
                    _logger.LogWarning("Verification token not found: {Token}", token);
                    return null;
                }

                var verificationData = JsonSerializer.Deserialize<EmailVerificationDataRedis>(json!);
                return verificationData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting verification data for token: {Token}", token);
                throw;
            }
        }

        public async Task<bool> CanSendVerificationEmailAsync(string email)
        {
            try
            {
                var rateLimitKey = RATE_LIMIT_PREFIX + email.ToLowerInvariant();
                var currentCount = await _database.StringGetAsync(rateLimitKey);

                if (!currentCount.HasValue)
                {
                    _logger.LogInformation("No rate limit data for email: {Email}", email);
                    return true;
                }

                int count = (int)currentCount;
                bool canSend = count < MAX_REQUESTS_PER_WINDOW;

                _logger.LogInformation(
                    "Rate limit check for {Email}: {Count}/{Max}, CanSend={CanSend}",
                    email, count, MAX_REQUESTS_PER_WINDOW, canSend);

                return canSend;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking rate limit for email: {Email}", email);
                // Fail open - allow request if Redis is down
                return true;
            }
        }

        public async Task StoreVerificationTokenAsync(string email, string token, Guid userId, TimeSpan expiry)
        {
            try
            {
                // Invalidate old token first
                await InvalidateVerificationTokenAsync(email);

                var verificationData = new EmailVerificationDataRedis
                {
                    Email = email,
                    UserId = userId,
                    Token = token,
                    CreatedAt = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(verificationData);
                var emailKey = TOKEN_BY_EMAIL_PREFIX + email.ToLowerInvariant();
                var tokenKey = TOKEN_DATA_PREFIX + token;

                // Use batch for atomic operations
                var batch = _database.CreateBatch();
                var emailTask = batch.StringSetAsync(emailKey, token, expiry);
                var tokenTask = batch.StringSetAsync(tokenKey, json, expiry);
                batch.Execute();

                await Task.WhenAll(emailTask, tokenTask);

                _logger.LogInformation(
                    "Stored verification token for email: {Email}, UserId: {UserId}, Expires in: {Expiry}",
                    email, userId, expiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing verification token for email: {Email}", email);
                throw;
            }
        }

        public async Task InvalidateVerificationTokenAsync(string email)
        {
            try
            {
                var emailKey = TOKEN_BY_EMAIL_PREFIX + email.ToLowerInvariant();
                var oldToken = await _database.StringGetAsync(emailKey);

                if (oldToken.HasValue)
                {
                    var tokenKey = TOKEN_DATA_PREFIX + oldToken;

                    var batch = _database.CreateBatch();
                    var deleteEmailTask = batch.KeyDeleteAsync(emailKey);
                    var deleteTokenTask = batch.KeyDeleteAsync(tokenKey);
                    batch.Execute();

                    await Task.WhenAll(deleteEmailTask, deleteTokenTask);

                    _logger.LogInformation("Invalidated verification token for email: {Email}", email);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating verification token for email: {Email}", email);
                throw;
            }
        }

        public async Task IncrementSendCountAsync(string email)
        {
            try
            {
                var rateLimitKey = RATE_LIMIT_PREFIX + email.ToLowerInvariant();
                var currentCount = await _database.StringGetAsync(rateLimitKey);

                if (!currentCount.HasValue)
                {
                    // First request - set count to 1 with expiry
                    await _database.StringSetAsync(rateLimitKey, 1, RATE_LIMIT_WINDOW);
                    _logger.LogInformation(
                        "Initialized rate limit for {Email}: 1/{Max}, Window: {Window}",
                        email, MAX_REQUESTS_PER_WINDOW, RATE_LIMIT_WINDOW);
                }
                else
                {
                    // Increment existing count
                    var newCount = await _database.StringIncrementAsync(rateLimitKey);
                    _logger.LogInformation(
                        "Incremented rate limit for {Email}: {Count}/{Max}",
                        email, newCount, MAX_REQUESTS_PER_WINDOW);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing send count for email: {Email}", email);
                throw;
            }
        }
    }
}
