using Microsoft.OpenApi.Validations;
using StackExchange.Redis;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Services.Interfaces;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service handling password reset token management using Redis cache
    /// Manages temporary reset tokens with expiration and rate limiting
    /// Token lifetime: 15 minutes
    /// Rate limit: 5 reset emails per 15 minutes per email address
    /// Storage: Redis with key prefixes for token data and rate limiting
    /// </summary>
    public class PasswordResetCacheService : IPasswordResetCacheService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _database;
        private readonly ILogger<PasswordResetCacheService> _logger;

        // Redis key prefixes
        private const string TOKEN_BY_EMAIL_PREFIX = "reset_password:token_by_email:";
        private const string TOKEN_DATA_PREFIX = "reset_password:token_data:";
        private const string RATE_LIMIT_PREFIX = "reset_password:rate_limit:";

        // Rate limit settings
        private const int MAX_REQUESTS_PER_WINDOW = 5;
        private static readonly TimeSpan RATE_LIMIT_WINDOW = TimeSpan.FromMinutes(15);

        public PasswordResetCacheService(
            IConnectionMultiplexer redis,
            ILogger<PasswordResetCacheService> logger)
        {
            _redis = redis;
            _database = redis.GetDatabase();
            _logger = logger;
        }

        /// <summary>
        /// Retrieve password reset data by token from Redis
        /// Returns: PasswordResetDataRedis if found and not expired, null otherwise
        /// Use case: Validate reset token when user clicks reset link or submits new password
        /// </summary>
        public async Task<PasswordResetDataRedis?> GetResetDataByTokenAsync(string token)
        {
            try
            {
                var tokenKey = TOKEN_DATA_PREFIX + token;
                var json = await _database.StringGetAsync(tokenKey);

                if (!json.HasValue)
                {
                    _logger.LogWarning("Reset token not found: {Token}", token);
                    return null;
                }

                var resetData = JsonSerializer.Deserialize<PasswordResetDataRedis>(json!);
                return resetData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting reset data for token: {Token}", token);
                throw;
            }
        }

        /// <summary>
        /// Check if user can send password reset email (rate limit check)
        /// Limit: 5 emails per 15 minutes per email address
        /// Returns: true if under limit, false if limit exceeded
        /// Fail-open: Returns true if Redis is unavailable
        /// </summary>
        public async Task<bool> CanSendResetEmailAsync(string email)
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

        /// <summary>
        /// Invalidate (delete) existing reset token for email
        /// Deletes both token-by-email and token-data keys
        /// Use case: Called before creating new token or after successful password reset
        /// </summary>
        public async Task InvalidateResetTokenAsync(string email)
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

                    _logger.LogInformation("Invalidated reset token for email: {Email}", email);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating reset token for email: {Email}", email);
                throw;
            }
        }

        /// <summary>
        /// Store password reset token in Redis
        /// Invalidates old token first (one active token per email)
        /// Keys:
        /// - "reset_password:token_by_email:{email}" → token
        /// - "reset_password:token_data:{token}" → JSON of PasswordResetDataRedis
        /// TTL: Specified expiry time (typically 15 minutes)
        /// Uses batch operation for atomicity
        /// </summary>
        public async Task StoreResetTokenAsync(string email, string token, Guid userId, TimeSpan expiry)
        {
            try
            {
                await InvalidateResetTokenAsync(email);

                var resetData = new PasswordResetDataRedis
                {
                    Email = email,
                    UserId = userId,
                    Token = token,
                    CreatedAt = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(resetData);
                var emailKey = TOKEN_BY_EMAIL_PREFIX + email.ToLowerInvariant();
                var tokenKey = TOKEN_DATA_PREFIX + token;

                var batch = _database.CreateBatch();
                var emailTask = batch.StringSetAsync(emailKey, token, expiry);
                var tokenTask = batch.StringSetAsync(tokenKey, json, expiry);
                batch.Execute();

                await Task.WhenAll(emailTask, tokenTask);

                _logger.LogInformation(
                    "Stored reset token for email: {Email}, UserId: {UserId}, Expires in: {Expiry}",
                    email, userId, expiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing reset token for email: {Email}", email);
                throw;
            }
        }

        /// <summary>
        /// Increment rate limit counter for password reset emails
        /// Key: "reset_password:rate_limit:{email}"
        /// First request: Sets counter to 1 with 15-minute expiry
        /// Subsequent requests: Increments counter (expiry remains unchanged)
        /// </summary>
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
