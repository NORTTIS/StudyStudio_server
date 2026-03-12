using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;
using System.Reflection;

namespace StudioStudio_Server.HealthChecks;

/// <summary>
/// Health check for Redis cache connectivity
/// </summary>
public class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisHealthCheck> _logger;

    public RedisHealthCheck(IConnectionMultiplexer redis, ILogger<RedisHealthCheck> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var startTime = DateTime.UtcNow;

            var db = _redis.GetDatabase();
            await db.PingAsync();

            var latency = (DateTime.UtcNow - startTime).TotalMilliseconds;

            // Get server info
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var serverInfo = await server.InfoAsync("server");

            _logger.LogDebug("Redis health check passed. Latency: {Latency}ms", latency);

            // Get redis version from server info
            var redisVersion = "unknown";
            foreach (var group in serverInfo)
            {
                if (group.Key == "redis_version")
                {
                    redisVersion = group.FirstOrDefault().Value ?? "unknown";
                    break;
                }
            }

            return HealthCheckResult.Healthy(
                $"Redis is healthy. Latency: {latency:F2}ms",
                data: new Dictionary<string, object>
                {
                    { "latency_ms", latency },
                    { "database", "Redis" },
                    { "version", redisVersion }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis health check failed");

            return HealthCheckResult.Unhealthy(
                $"Redis health check failed: {ex.Message}",
                exception: ex);
        }
    }
}
