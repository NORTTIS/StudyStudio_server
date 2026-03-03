using Microsoft.Extensions.DependencyInjection;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services.EmbeddingQueue
{
    /// <summary>
    /// Background service that processes embedding jobs from the queue with token-aware rate limiting
    /// 
    /// Processing Strategy:
    /// 1. Dequeue job from queue
    /// 2. Check token budget (800K tokens/minute limit)
    /// 3. If budget available: Process immediately
    /// 4. If budget exceeded: Wait until window resets
    /// 5. Update status throughout processing
    /// 
    /// Rate Limiting:
    /// - Target: 800K tokens/minute (80% of 1M TPM, 20% safety margin)
    /// - Automatic waiting when limit approached
    /// - No manual delays needed between jobs
    /// </summary>
    public class EmbeddingBackgroundService : BackgroundService
    {
        private readonly IEmbeddingQueue _queue;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<EmbeddingBackgroundService> _logger;
        
        private int _processedJobsCount = 0;
        private int _failedJobsCount = 0;

        public EmbeddingBackgroundService(
            IEmbeddingQueue queue,
            IServiceScopeFactory serviceScopeFactory,
            ILogger<EmbeddingBackgroundService> logger)
        {
            _queue = queue;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "?? Embedding Background Service started. " +
                "Token limit: 800K/minute, Batch processing enabled");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Wait for next job
                    var job = await _queue.DequeueAsync(stoppingToken);
                    
                    _logger.LogInformation(
                        "?? Processing job: {AttachmentId}, File: {FileName}, " +
                        "Estimated tokens: {Tokens:N0}, Attempt: {Retry}/{Max}",
                        job.AttachmentId, job.FileName, 
                        job.EstimatedTokens, job.RetryCount + 1, job.MaxRetries);

                    // Token-based rate limiting: Wait if budget exceeded
                    bool canProcess = await _queue.TryReserveTokensAsync(job.EstimatedTokens, stoppingToken);
                    
                    while (!canProcess && !stoppingToken.IsCancellationRequested)
                    {
                        var budget = _queue.GetTokenBudget();
                        
                        _logger.LogWarning(
                            "??  Token budget exceeded. Waiting {Seconds:F0}s until reset. " +
                            "Current usage: {Used:N0}/{Max:N0} tokens ({Percent:F1}%)",
                            budget.TimeUntilReset.TotalSeconds,
                            budget.TokensUsedThisMinute,
                            budget.MaxTokensPerMinute,
                            budget.UtilizationPercent);
                        
                        // Wait until window resets (add small buffer)
                        await Task.Delay(budget.TimeUntilReset + TimeSpan.FromSeconds(1), stoppingToken);
                        
                        // Try again
                        canProcess = await _queue.TryReserveTokensAsync(job.EstimatedTokens, stoppingToken);
                    }

                    // Create a new scope for scoped services
                    using var scope = _serviceScopeFactory.CreateScope();
                    
                    var attachmentRepository = scope.ServiceProvider.GetRequiredService<IGroupAttachmentRepository>();
                    var fileStorageService = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
                    var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
                    var vectorDbService = scope.ServiceProvider.GetRequiredService<IVectorDatabaseService>();
                    var documentService = scope.ServiceProvider.GetRequiredService<IDocumentService>();

                    try
                    {
                        // Process the document
                        await documentService.ProcessDocumentAsync(
                            job.AttachmentId,
                            attachmentRepository,
                            fileStorageService,
                            embeddingService,
                            vectorDbService,
                            _logger);

                        _queue.UpdateJobStatus(job.AttachmentId, EmbeddingJobStatus.Completed);
                        
                        _processedJobsCount++;
                        
                        _logger.LogInformation(
                            "? Successfully processed: {AttachmentId}. " +
                            "Stats: Processed={Processed}, Failed={Failed}, Queue={Queue}",
                            job.AttachmentId, _processedJobsCount, _failedJobsCount, _queue.GetQueueDepth());
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, 
                            "? Failed to process: {AttachmentId} (Attempt {Retry}/{Max})",
                            job.AttachmentId, job.RetryCount + 1, job.MaxRetries);

                        // Release reserved tokens since job failed
                        _queue.ReleaseTokens(job.EstimatedTokens);

                        // Retry logic
                        if (job.RetryCount < job.MaxRetries)
                        {
                            job.RetryCount++;
                            
                            // Exponential backoff before retry
                            int delaySeconds = (int)Math.Pow(2, job.RetryCount) * 5; // 5s, 10s, 20s
                            
                            _logger.LogWarning(
                                "?? Retrying job: {AttachmentId} in {Delay}s (Attempt {Retry}/{Max})",
                                job.AttachmentId, delaySeconds, job.RetryCount + 1, job.MaxRetries);
                            
                            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
                            
                            // Re-queue the job
                            await _queue.EnqueueAsync(job, stoppingToken);
                        }
                        else
                        {
                            _queue.UpdateJobStatus(job.AttachmentId, EmbeddingJobStatus.Failed, ex.Message);
                            _failedJobsCount++;
                            
                            _logger.LogError(
                                "?? Job failed permanently: {AttachmentId} after {Retries} attempts",
                                job.AttachmentId, job.MaxRetries);
                        }
                    }

                    // No fixed delay needed - token budget controls rate automatically
                    // Optional: Small delay to prevent CPU spinning in edge cases
                    await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Normal shutdown
                    _logger.LogInformation("?? Embedding Background Service shutting down gracefully");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "??  Unexpected error in embedding background service");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }

            _logger.LogInformation(
                "?? Embedding Background Service stopped. " +
                "Final stats: Processed={Processed}, Failed={Failed}",
                _processedJobsCount, _failedJobsCount);
        }
    }
}
