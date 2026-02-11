using Microsoft.OpenApi.Validations;
using StackExchange.Redis;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Services.Interfaces;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StudioStudio_Server.Services
{
    public class PasswordResetCacheService : IPasswordResetCacheService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _database;
        private const string TOKEN_BY_EMAIL_PREFIX = "reset_password:token_by_email:";
        private const string TOKEN_DATA_PREFIX = "reset_password:token_data:";

        public PasswordResetCacheService(IConnectionMultiplexer redis)
        {
            _redis = redis;
            _database = redis.GetDatabase();
        }
        public async Task<PasswordResetDataRedis?> GetResetDataByTokenAsync(string token)
        {
            var tokenKey = TOKEN_DATA_PREFIX + token;
            var json = await _database.StringGetAsync(tokenKey);

            if (!json.HasValue)
            {
                return null;
            }

            var resetData = JsonSerializer.Deserialize<PasswordResetDataRedis>(json!);
            return resetData;
        }

        public async Task InvalidateResetTokenAsync(string email)
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
            }
        }

        public async Task StoreResetTokenAsync(string email, string token, Guid userId, TimeSpan expiry)
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
            var batch = _database.CreateBatch();

            var emailKey = TOKEN_BY_EMAIL_PREFIX + email.ToLowerInvariant();
            var emailTask = batch.StringSetAsync(emailKey, token, expiry);

            var tokenKey = TOKEN_DATA_PREFIX + token;
            var tokenTask = batch.StringSetAsync(tokenKey, json, expiry);

            batch.Execute();
            await Task.WhenAll(emailTask, tokenTask);
        }
    }
}
