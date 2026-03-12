using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StudioStudio_Server.Data;
using System.Data.Common;

namespace StudioStudio_Server.HealthChecks;

/// <summary>
/// Health check for PostgreSQL database connectivity
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly StudioDbContext _dbContext;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(StudioDbContext dbContext, ILogger<DatabaseHealthCheck> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var startTime = DateTime.UtcNow;

            // Test database connectivity
            var connection = _dbContext.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);

            // Execute a simple query to test read capability
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);

            var latency = (DateTime.UtcNow - startTime).TotalMilliseconds;

            await connection.CloseAsync();

            _logger.LogDebug("Database health check passed. Latency: {Latency}ms", latency);

            return HealthCheckResult.Healthy(
                $"Database is healthy. Latency: {latency:F2}ms",
                data: new Dictionary<string, object>
                {
                    { "latency_ms", latency },
                    { "database", "PostgreSQL" }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");

            return HealthCheckResult.Unhealthy(
                $"Database health check failed: {ex.Message}",
                exception: ex);
        }
    }
}
