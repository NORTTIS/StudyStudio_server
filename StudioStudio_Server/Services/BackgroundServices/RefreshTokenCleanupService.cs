using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Services.BackgroundServices
{
    /// <summary>
    /// Background service to periodically clean up expired and revoked refresh tokens
    /// Runs every 24 hours to prevent database bloat
    /// Removes tokens where: IsRevoked = true OR ExpiresAt < UtcNow
    /// </summary>
    public class RefreshTokenCleanupService(
        IServiceProvider serviceProvider,
        ILogger<RefreshTokenCleanupService> logger) : BackgroundService
    {
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(24);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("?? Refresh Token Cleanup Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(CleanupInterval, stoppingToken);
                    
                    using var scope = serviceProvider.CreateScope();
                    var refreshTokenRepository = scope.ServiceProvider
                        .GetRequiredService<IRefreshTokenRepository>();

                    logger.LogInformation("?? Starting refresh token cleanup...");
                    
                    var deletedCount = await refreshTokenRepository.CleanupExpiredTokensAsync();
                    
                    logger.LogInformation(
                        "? Refresh token cleanup completed. Deleted {Count} expired/revoked tokens",
                        deletedCount);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "? Error during refresh token cleanup");
                }
            }

            logger.LogInformation("?? Refresh Token Cleanup Service stopped");
        }
    }
}
