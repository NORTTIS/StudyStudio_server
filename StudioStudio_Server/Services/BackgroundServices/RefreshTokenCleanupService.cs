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
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly ILogger<RefreshTokenCleanupService> _logger = logger;
        private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(24);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("?? Refresh Token Cleanup Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(CleanupInterval, stoppingToken);
                    
                    using var scope = _serviceProvider.CreateScope();
                    var refreshTokenRepository = scope.ServiceProvider
                        .GetRequiredService<IRefreshTokenRepository>();

                    _logger.LogInformation("?? Starting refresh token cleanup...");
                    
                    var deletedCount = await refreshTokenRepository.CleanupExpiredTokensAsync();
                    
                    _logger.LogInformation(
                        "? Refresh token cleanup completed. Deleted {Count} expired/revoked tokens",
                        deletedCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "? Error during refresh token cleanup");
                }
            }

            _logger.LogInformation("?? Refresh Token Cleanup Service stopped");
        }
    }
}
