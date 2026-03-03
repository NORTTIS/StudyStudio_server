using System.Threading.Channels;

namespace StudioStudio_Server.Services.EmbeddingQueue
{
    /// <summary>
    /// Token budget tracker for rate limiting
    /// Tracks token usage per minute to respect TPM (Token Per Minute) limits
    /// </summary>
    public class TokenBudget
    {
        public int TokensUsedThisMinute { get; set; }
        public int MaxTokensPerMinute { get; set; } = 800_000; // 80% of 1M (20% safety margin)
        public int RemainingTokens => Math.Max(0, MaxTokensPerMinute - TokensUsedThisMinute);
        public double UtilizationPercent => (double)TokensUsedThisMinute / MaxTokensPerMinute * 100;
        public DateTime WindowStartTime { get; set; }
        public DateTime WindowEndTime => WindowStartTime.AddMinutes(1);
        public TimeSpan TimeUntilReset => WindowEndTime - DateTime.UtcNow;
        public bool CanAccommodate(int tokens) => RemainingTokens >= tokens;
    }

    /// <summary>
    /// Interface for managing embedding job queue with token-aware rate limiting
    /// </summary>
    public interface IEmbeddingQueue
    {
        /// <summary>
        /// Enqueue a new embedding job
        /// </summary>
        ValueTask EnqueueAsync(EmbeddingJob job, CancellationToken cancellationToken = default);

        /// <summary>
        /// Dequeue next job for processing
        /// </summary>
        ValueTask<EmbeddingJob> DequeueAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get current job status
        /// </summary>
        EmbeddingJobStatusInfo? GetJobStatus(Guid attachmentId);

        /// <summary>
        /// Update job status
        /// </summary>
        void UpdateJobStatus(Guid attachmentId, EmbeddingJobStatus status, string? errorMessage = null, int processedChunks = 0, int totalChunks = 0);

        /// <summary>
        /// Get queue depth
        /// </summary>
        int GetQueueDepth();
        
        /// <summary>
        /// Get current token budget
        /// </summary>
        TokenBudget GetTokenBudget();
        
        /// <summary>
        /// Check if we can process a job with given token count
        /// Returns false if budget exceeded, caller should wait
        /// </summary>
        Task<bool> TryReserveTokensAsync(int estimatedTokens, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Release unused tokens back to budget (e.g., if job fails early)
        /// </summary>
        void ReleaseTokens(int tokens);
        
        /// <summary>
        /// Update actual token usage after processing
        /// </summary>
        void UpdateActualTokens(Guid attachmentId, int actualTokens);
    }

    /// <summary>
    /// Channel-based implementation of embedding queue with token-aware rate limiting
    /// High-performance, thread-safe queue using System.Threading.Channels
    /// 
    /// Rate Limiting Strategy:
    /// - Target: 800K tokens/minute (80% of 1M TPM limit, 20% safety margin)
    /// - Tracks token usage in sliding 1-minute windows
    /// - Pauses processing when approaching limit
    /// - Automatically resets window after 1 minute
    /// </summary>
    public class EmbeddingQueue : IEmbeddingQueue
    {
        private readonly Channel<EmbeddingJob> _queue;
        private readonly Dictionary<Guid, EmbeddingJobStatusInfo> _jobStatuses = new();
        private readonly SemaphoreSlim _statusLock = new(1, 1);
        private readonly SemaphoreSlim _tokenLock = new(1, 1);
        private readonly ILogger<EmbeddingQueue> _logger;
        private int _queueDepth = 0;
        
        // Token tracking for rate limiting
        private DateTime _tokenWindowStart = DateTime.UtcNow;
        private int _tokensUsedThisWindow = 0;
        private const int MAX_TOKENS_PER_MINUTE = 800_000; // 80% of 1M (safety margin)

        public EmbeddingQueue(ILogger<EmbeddingQueue> logger)
        {
            _logger = logger;
            
            // Unbounded channel - queues all jobs
            var options = new UnboundedChannelOptions
            {
                SingleReader = true,  // Only one background service reading
                SingleWriter = false  // Multiple controllers can enqueue
            };
            
            _queue = Channel.CreateUnbounded<EmbeddingJob>(options);
        }

        public async ValueTask EnqueueAsync(EmbeddingJob job, CancellationToken cancellationToken = default)
        {
            await _statusLock.WaitAsync(cancellationToken);
            try
            {
                // Initialize job status
                _jobStatuses[job.AttachmentId] = new EmbeddingJobStatusInfo
                {
                    AttachmentId = job.AttachmentId,
                    Status = EmbeddingJobStatus.Queued,
                    QueuedAt = job.QueuedAt,
                    RetryCount = job.RetryCount,
                    EstimatedTokens = job.EstimatedTokens
                };

                await _queue.Writer.WriteAsync(job, cancellationToken);
                
                Interlocked.Increment(ref _queueDepth);
                
                _logger.LogInformation(
                    "Enqueued embedding job: {AttachmentId}, File: {FileName}, " +
                    "Estimated tokens: {Tokens:N0}, Queue depth: {Depth}",
                    job.AttachmentId, job.FileName, job.EstimatedTokens, _queueDepth);
            }
            finally
            {
                _statusLock.Release();
            }
        }

        public async ValueTask<EmbeddingJob> DequeueAsync(CancellationToken cancellationToken = default)
        {
            var job = await _queue.Reader.ReadAsync(cancellationToken);
            
            Interlocked.Decrement(ref _queueDepth);
            
            await _statusLock.WaitAsync(cancellationToken);
            try
            {
                if (_jobStatuses.TryGetValue(job.AttachmentId, out var status))
                {
                    status.Status = EmbeddingJobStatus.Processing;
                    status.StartedAt = DateTime.UtcNow;
                }
            }
            finally
            {
                _statusLock.Release();
            }

            _logger.LogInformation(
                "Dequeued job: {AttachmentId}, Remaining in queue: {Depth}",
                job.AttachmentId, _queueDepth);

            return job;
        }

        public EmbeddingJobStatusInfo? GetJobStatus(Guid attachmentId)
        {
            _statusLock.Wait();
            try
            {
                return _jobStatuses.TryGetValue(attachmentId, out var status) ? status : null;
            }
            finally
            {
                _statusLock.Release();
            }
        }

        public void UpdateJobStatus(
            Guid attachmentId, 
            EmbeddingJobStatus status, 
            string? errorMessage = null, 
            int processedChunks = 0, 
            int totalChunks = 0)
        {
            _statusLock.Wait();
            try
            {
                if (_jobStatuses.TryGetValue(attachmentId, out var jobStatus))
                {
                    jobStatus.Status = status;
                    jobStatus.ErrorMessage = errorMessage;
                    jobStatus.ProcessedChunks = processedChunks;
                    
                    if (totalChunks > 0)
                    {
                        jobStatus.TotalChunks = totalChunks;
                    }
                    
                    if (status == EmbeddingJobStatus.Completed || status == EmbeddingJobStatus.Failed)
                    {
                        jobStatus.CompletedAt = DateTime.UtcNow;
                    }
                    
                    _logger.LogDebug(
                        "Job status updated: {AttachmentId}, Status: {Status}, Progress: {Progress}%",
                        attachmentId, status, jobStatus.Progress);
                }
            }
            finally
            {
                _statusLock.Release();
            }
        }

        public int GetQueueDepth() => _queueDepth;

        public TokenBudget GetTokenBudget()
        {
            _tokenLock.Wait();
            try
            {
                ResetTokenWindowIfNeeded();
                
                return new TokenBudget
                {
                    TokensUsedThisMinute = _tokensUsedThisWindow,
                    MaxTokensPerMinute = MAX_TOKENS_PER_MINUTE,
                    WindowStartTime = _tokenWindowStart
                };
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        public async Task<bool> TryReserveTokensAsync(int estimatedTokens, CancellationToken cancellationToken = default)
        {
            await _tokenLock.WaitAsync(cancellationToken);
            try
            {
                ResetTokenWindowIfNeeded();
                
                int remainingTokens = MAX_TOKENS_PER_MINUTE - _tokensUsedThisWindow;
                
                if (estimatedTokens <= remainingTokens)
                {
                    // Reserve tokens
                    _tokensUsedThisWindow += estimatedTokens;
                    
                    _logger.LogInformation(
                        "Token budget reserved: {Tokens:N0} tokens, " +
                        "Used: {Used:N0}/{Max:N0} ({Percent:F1}%), " +
                        "Remaining: {Remaining:N0}",
                        estimatedTokens,
                        _tokensUsedThisWindow,
                        MAX_TOKENS_PER_MINUTE,
                        (double)_tokensUsedThisWindow / MAX_TOKENS_PER_MINUTE * 100,
                        MAX_TOKENS_PER_MINUTE - _tokensUsedThisWindow);
                    
                    return true;
                }
                else
                {
                    // Budget exceeded
                    TimeSpan waitTime = _tokenWindowStart.AddMinutes(1) - DateTime.UtcNow;
                    
                    _logger.LogWarning(
                        "?? Token budget exceeded! Need {Need:N0} tokens, have {Have:N0} remaining. " +
                        "Waiting {Seconds:F1}s until window reset",
                        estimatedTokens,
                        remainingTokens,
                        waitTime.TotalSeconds);
                    
                    return false;
                }
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        public void ReleaseTokens(int tokens)
        {
            _tokenLock.Wait();
            try
            {
                _tokensUsedThisWindow = Math.Max(0, _tokensUsedThisWindow - tokens);
                
                _logger.LogDebug("Released {Tokens:N0} tokens back to budget", tokens);
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        public void UpdateActualTokens(Guid attachmentId, int actualTokens)
        {
            _statusLock.Wait();
            try
            {
                if (_jobStatuses.TryGetValue(attachmentId, out var status))
                {
                    int estimated = status.EstimatedTokens;
                    int difference = actualTokens - estimated;
                    double accuracy = estimated > 0 ? (double)actualTokens / estimated * 100 : 0;
                    
                    _logger.LogInformation(
                        "Token usage updated: {AttachmentId}, " +
                        "Estimated: {Estimated:N0}, Actual: {Actual:N0}, " +
                        "Difference: {Diff:+#,0;-#,0;0} ({Accuracy:F1}% of estimate)",
                        attachmentId, estimated, actualTokens, difference, accuracy);
                }
            }
            finally
            {
                _statusLock.Release();
            }
        }

        private void ResetTokenWindowIfNeeded()
        {
            DateTime now = DateTime.UtcNow;
            TimeSpan elapsed = now - _tokenWindowStart;
            
            if (elapsed.TotalMinutes >= 1)
            {
                _logger.LogInformation(
                    "?? Token window reset. Previous usage: {Used:N0}/{Max:N0} tokens ({Percent:F1}%)",
                    _tokensUsedThisWindow,
                    MAX_TOKENS_PER_MINUTE,
                    (double)_tokensUsedThisWindow / MAX_TOKENS_PER_MINUTE * 100);
                
                _tokenWindowStart = now;
                _tokensUsedThisWindow = 0;
            }
        }
    }
}
