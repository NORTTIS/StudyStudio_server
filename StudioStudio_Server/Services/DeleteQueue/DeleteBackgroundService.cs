using StudioStudio_Server.Services.Interfaces;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace StudioStudio_Server.Services.DeleteQueue
{
    /// <summary>
    /// Background service that processes vector deletion jobs from the queue
    /// 
    /// Processing Strategy:
    /// 1. Dequeue job from queue
    /// 2. Delete each vector with retry logic (3 attempts, exponential backoff)
    /// 3. Track success/failure counts
    /// 4. Update status throughout processing
    /// 5. Handle partial failures gracefully
    /// 
    /// Retry Logic:
    /// - Each vector deletion retried up to 3 times
    /// - Exponential backoff: 1s ? 2s ? 4s
    /// - Timeouts and HTTP errors trigger retry
    /// </summary>
    public class DeleteBackgroundService(
        IDeleteQueue queue,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<DeleteBackgroundService> logger) : BackgroundService
    {
        private readonly IDeleteQueue _queue = queue;
        private readonly IServiceScopeFactory _serviceScopeFactory = serviceScopeFactory;
        private readonly ILogger<DeleteBackgroundService> _logger = logger;

        private int _processedJobsCount = 0;
        private int _failedJobsCount = 0;
        private int _partialJobsCount = 0;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "??? Delete Background Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Wait for next job
                    var job = await _queue.DequeueAsync(stoppingToken);

                    _logger.LogInformation(
                        "?? Processing delete job: AttachmentId={AttachmentId}, File={FileName}, " +
                        "Chunks={ChunkCount}, Attempt={Retry}/{Max}",
                        job.AttachmentId, job.FileName, job.ChunkCount,
                        job.RetryCount + 1, job.MaxRetries);

                    Stopwatch sw = Stopwatch.StartNew();

                    // Create a new scope for scoped services
                    using var scope = _serviceScopeFactory.CreateScope();
                    var vectorDbService = scope.ServiceProvider.GetRequiredService<IVectorDatabaseService>();

                    try
                    {
                        // Delete all vectors for this document
                        int successCount = 0;
                        int failureCount = 0;

                        for (int i = 0; i < job.ChunkCount; i++)
                        {
                            // Use same ID format as when upserting
                            string rawId = $"{job.AttachmentId}_{i}";
                            string vectorId = GenerateDeterministicUuid(rawId);

                            bool deleted = await DeleteVectorWithRetryAsync(
                                vectorDbService,
                                vectorId,
                                maxRetries: 3,
                                stoppingToken);

                            if (deleted)
                            {
                                successCount++;
                            }
                            else
                            {
                                failureCount++;
                                _logger.LogWarning(
                                    "Failed to delete vector after retries. " +
                                    "VectorId={VectorId}, AttachmentId={AttachmentId}, ChunkIndex={ChunkIndex}",
                                    vectorId, job.AttachmentId, i);
                            }

                            // Update progress every 5 chunks or on last chunk
                            if ((i + 1) % 5 == 0 || i == job.ChunkCount - 1)
                            {
                                _queue.UpdateJobStatus(
                                    job.AttachmentId,
                                    DeleteJobStatus.Processing,
                                    null,
                                    successCount,
                                    failureCount);
                            }
                        }

                        sw.Stop();

                        // Determine final status
                        DeleteJobStatus finalStatus;
                        if (failureCount == 0)
                        {
                            finalStatus = DeleteJobStatus.Completed;
                            _processedJobsCount++;
                            _logger.LogInformation(
                                "? Delete job completed successfully: AttachmentId={AttachmentId}, " +
                                "Deleted={Success}/{Total} vectors in {Ms}ms",
                                job.AttachmentId, successCount, job.ChunkCount, sw.ElapsedMilliseconds);
                        }
                        else if (successCount > 0)
                        {
                            finalStatus = DeleteJobStatus.PartiallyCompleted;
                            _partialJobsCount++;
                            _logger.LogWarning(
                                "?? Delete job partially completed: AttachmentId={AttachmentId}, " +
                                "Deleted={Success}/{Total}, Failed={Failed} in {Ms}ms",
                                job.AttachmentId, successCount, job.ChunkCount, failureCount, sw.ElapsedMilliseconds);
                        }
                        else
                        {
                            finalStatus = DeleteJobStatus.Failed;
                            _failedJobsCount++;
                            _logger.LogError(
                                "? Delete job failed completely: AttachmentId={AttachmentId}, " +
                                "All {Total} vectors failed to delete",
                                job.AttachmentId, job.ChunkCount);
                        }

                        _queue.UpdateJobStatus(
                            job.AttachmentId,
                            finalStatus,
                            null,
                            successCount,
                            failureCount);

                        _logger.LogInformation(
                            "?? Delete service stats: Completed={Completed}, Partial={Partial}, " +
                            "Failed={Failed}, Queue={Queue}",
                            _processedJobsCount, _partialJobsCount, _failedJobsCount, _queue.GetQueueDepth());
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "? Failed to process delete job: AttachmentId={AttachmentId} (Attempt {Retry}/{Max})",
                            job.AttachmentId, job.RetryCount + 1, job.MaxRetries);

                        // Retry logic for entire job
                        if (job.RetryCount < job.MaxRetries)
                        {
                            job.RetryCount++;

                            // Exponential backoff before retry
                            int delaySeconds = (int)Math.Pow(2, job.RetryCount) * 5; // 5s, 10s, 20s

                            _logger.LogWarning(
                                "?? Retrying delete job: AttachmentId={AttachmentId} in {Delay}s (Attempt {Retry}/{Max})",
                                job.AttachmentId, delaySeconds, job.RetryCount + 1, job.MaxRetries);

                            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);

                            // Re-queue the job
                            await _queue.EnqueueAsync(job, stoppingToken);
                        }
                        else
                        {
                            _queue.UpdateJobStatus(
                                job.AttachmentId,
                                DeleteJobStatus.Failed,
                                ex.Message);
                            _failedJobsCount++;

                            _logger.LogError(
                                "? Delete job failed permanently: AttachmentId={AttachmentId} after {Retries} attempts",
                                job.AttachmentId, job.MaxRetries);
                        }
                    }

                    // Small delay to prevent CPU spinning
                    await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Normal shutdown
                    _logger.LogInformation("?? Delete Background Service shutting down gracefully");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "? Unexpected error in delete background service");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }

            _logger.LogInformation(
                "?? Delete Background Service stopped. " +
                "Final stats: Completed={Completed}, Partial={Partial}, Failed={Failed}",
                _processedJobsCount, _partialJobsCount, _failedJobsCount);
        }

        /// <summary>
        /// Delete vector from Qdrant with retry logic
        /// Retries on timeout or transient errors (500, 502, 503, 504)
        /// Uses exponential backoff: 1s, 2s, 4s
        /// </summary>
        private async Task<bool> DeleteVectorWithRetryAsync(
            IVectorDatabaseService vectorDbService,
            string vectorId,
            int maxRetries,
            CancellationToken cancellationToken)
        {
            int retryCount = 0;
            int delayMs = 1000; // Start with 1 second

            while (retryCount < maxRetries && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    bool result = await vectorDbService.DeleteVectorAsync(vectorId);

                    if (result)
                    {
                        if (retryCount > 0)
                        {
                            _logger.LogInformation(
                                "Vector deleted successfully after {RetryCount} retries. VectorId={VectorId}",
                                retryCount, vectorId);
                        }
                        return true;
                    }

                    // If delete returns false but no exception, consider it a failure
                    _logger.LogWarning(
                        "Vector delete returned false (attempt {Attempt}/{Max}). VectorId={VectorId}",
                        retryCount + 1, maxRetries, vectorId);
                }
                catch (TaskCanceledException ex)
                {
                    // Timeout exception
                    _logger.LogWarning(
                        "Vector delete timeout (attempt {Attempt}/{Max}). VectorId={VectorId}, Error={Error}",
                        retryCount + 1, maxRetries, vectorId, ex.Message);
                }
                catch (HttpRequestException ex)
                {
                    // HTTP errors (network issues, 500, 502, 503, 504)
                    _logger.LogWarning(
                        "Vector delete HTTP error (attempt {Attempt}/{Max}). VectorId={VectorId}, Error={Error}",
                        retryCount + 1, maxRetries, vectorId, ex.Message);
                }
                catch (Exception ex)
                {
                    // Other unexpected errors - log and retry
                    _logger.LogWarning(ex,
                        "Vector delete unexpected error (attempt {Attempt}/{Max}). VectorId={VectorId}",
                        retryCount + 1, maxRetries, vectorId);
                }

                retryCount++;

                // If not last retry, wait before retrying (exponential backoff)
                if (retryCount < maxRetries && !cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation(
                        "Retrying vector delete in {DelayMs}ms. VectorId={VectorId}",
                        delayMs, vectorId);

                    await Task.Delay(delayMs, cancellationToken);
                    delayMs *= 2; // Exponential backoff: 1s -> 2s -> 4s
                }
            }

            // All retries exhausted or cancelled
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Vector delete cancelled. VectorId={VectorId}", vectorId);
            }
            else
            {
                _logger.LogError(
                    "Failed to delete vector after {MaxRetries} attempts. VectorId={VectorId}",
                    maxRetries, vectorId);
            }

            return false;
        }

        /// <summary>
        /// Generate deterministic UUID from string (same as in DocumentService)
        /// </summary>
        private string GenerateDeterministicUuid(string input)
        {
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            return new Guid(hash).ToString();
        }
    }
}
