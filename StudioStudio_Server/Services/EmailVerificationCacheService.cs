using StackExchange.Redis;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Services.Interfaces;
using System.Text.Json;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service handling email verification token management using Redis cache
    /// Manages temporary verification tokens for user registration and email changes
    /// Token lifetime: 15 minutes
    /// Rate limit: 5 verification emails per 15 minutes per email address
    /// Storage: Redis with key prefixes for token data and rate limiting
    /// </summary>
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

        /// <summary>
        /// Retrieve email verification data by token from Redis
        /// Returns: EmailVerificationDataRedis if found and not expired, null otherwise
        /// Use case: Validate verification token when user clicks verification link
        /// </summary>
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

        /// <summary>
        /// Check if user can send verification email (rate limit check)
        /// Limit: 5 emails per 15 minutes per email address
        /// Returns: true if under limit, false if limit exceeded
        /// Fail-open: Returns true if Redis is unavailable (allows request)
        /// </summary>
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

        /// <summary>
        /// Store email verification token in Redis
        /// Invalidates old token first (one active token per email)
        /// Keys:
        /// - "email_verification:token_by_email:{email}" ? token
        /// - "email_verification:token_data:{token}" ? JSON of EmailVerificationDataRedis
        /// TTL: Specified expiry time (typically 15 minutes)
        /// Uses batch operation for atomicity
        /// </summary>
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

        /// <summary>
        /// Invalidate (delete) existing verification token for email
        /// Deletes both token-by-email and token-data keys
        /// Use case: Called before creating new token or after successful verification
        /// </summary>
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

        /// <summary>
        /// Increment rate limit counter for verification emails
        /// Key: "email_verification:rate_limit:{email}"
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
