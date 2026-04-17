using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services.CleanupQueue
{
    /// <summary>
    /// Background service that periodically cleans up stuck uploads.
    /// Documents stuck in "Uploading" status for more than 30 minutes are considered abandoned
    /// (frontend crash, expired presigned URL, etc.) and are hard-deleted.
    ///
    /// Run interval: Every 15 minutes
    /// </summary>
    public class CleanupBackgroundService(
        ICleanupQueue queue,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<CleanupBackgroundService> logger) : BackgroundService
    {
        private readonly ICleanupQueue _queue = queue;
        private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
        private readonly ILogger<CleanupBackgroundService> _logger = logger;

        private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan StuckThreshold = TimeSpan.FromMinutes(30);

        private int _processedCount = 0;
        private int _failedCount = 0;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Cleanup Background Service started. Scan interval: {Interval}min, Stuck threshold: {Threshold}min",
                ScanInterval.TotalMinutes, StuckThreshold.TotalMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ScanAndEnqueueStuckUploadsAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Cleanup Background Service shutting down gracefully");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error scanning for stuck uploads");
                }

                try
                {
                    await Task.Delay(ScanInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation(
                "Cleanup Background Service stopped. Final stats: Processed={Processed}, Failed={Failed}",
                _processedCount, _failedCount);
        }

        /// <summary>
        /// Scan database for stuck uploads and enqueue cleanup jobs
        /// </summary>
        private async Task ScanAndEnqueueStuckUploadsAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var attachmentRepository = scope.ServiceProvider.GetRequiredService<IGroupAttachmentRepository>();

            _logger.LogInformation("Scanning for stuck uploads older than {Threshold} minutes...", StuckThreshold.TotalMinutes);

            var stuckUploads = await attachmentRepository.GetStuckUploadsAsync(StuckThreshold);

            if (stuckUploads.Count == 0)
            {
                _logger.LogInformation("No stuck uploads found");
                return;
            }

            _logger.LogInformation("Found {Count} stuck uploads to clean up", stuckUploads.Count);

            foreach (var upload in stuckUploads)
            {
                try
                {
                    var job = new StuckUploadJob
                    {
                        AttachmentId = upload.GroupAttachmentId,
                        GroupId = upload.GroupId,
                        FileKey = upload.FileUrl,
                        FileName = upload.FileName,
                        FileSize = upload.FileSize,
                        UploadedAt = upload.UploadedAt
                    };

                    await _queue.EnqueueAsync(job, stoppingToken);
                    _processedCount++;
                }
                catch (Exception ex)
                {
                    _failedCount++;
                    _logger.LogError(ex,
                        "Failed to enqueue stuck upload cleanup job: AttachmentId={AttachmentId}",
                        upload.GroupAttachmentId);
                }
            }

            // Process queued jobs immediately (don't wait for next scan cycle)
            await ProcessCleanupQueueAsync(stoppingToken);
        }

        /// <summary>
        /// Process cleanup jobs from the queue
        /// </summary>
        private async Task ProcessCleanupQueueAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested && _queue.GetQueueDepth() > 0)
            {
                try
                {
                    var job = await _queue.DequeueAsync(stoppingToken);

                    _logger.LogInformation(
                        "Processing stuck upload cleanup: AttachmentId={AttachmentId}, FileKey={FileKey}, FileSize={FileSize}",
                        job.AttachmentId, job.FileKey, job.FileSize);

                    using var scope = _serviceScopeFactory.CreateScope();
                    var attachmentRepository = scope.ServiceProvider.GetRequiredService<IGroupAttachmentRepository>();
                    var fileStorageService = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

                    // Delete B2 file
                    try
                    {
                        await fileStorageService.DeleteFileAsync(job.FileKey);
                        _logger.LogInformation("B2 blob deleted: AttachmentId={AttachmentId}, FileKey={FileKey}",
                            job.AttachmentId, job.FileKey);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Failed to delete B2 blob for stuck upload: AttachmentId={AttachmentId}. Continuing with DB cleanup.",
                            job.AttachmentId);
                    }

                    // Hard-delete DB record
                    await attachmentRepository.HardDeleteAsync(job.AttachmentId);

                    _logger.LogInformation(
                        "Stuck upload cleaned up: AttachmentId={AttachmentId}, FileSize={FileSize}",
                        job.AttachmentId, job.FileSize);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _failedCount++;
                    _logger.LogError(ex, "Error processing stuck upload cleanup job");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }
    }
}
