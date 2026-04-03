using System.Threading.Channels;

namespace StudioStudio_Server.Services.CleanupQueue
{
    /// <summary>
    /// Interface for stuck upload cleanup queue
    /// </summary>
    public interface ICleanupQueue
    {
        /// <summary>
        /// Enqueue a stuck upload cleanup job
        /// </summary>
        ValueTask EnqueueAsync(StuckUploadJob job, CancellationToken cancellationToken = default);

        /// <summary>
        /// Dequeue next job for processing
        /// </summary>
        ValueTask<StuckUploadJob> DequeueAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get current queue depth
        /// </summary>
        int GetQueueDepth();
    }

    /// <summary>
    /// Channel-based implementation of stuck upload cleanup queue
    /// </summary>
    public class CleanupQueue : ICleanupQueue, IDisposable
    {
        private readonly Channel<StuckUploadJob> _queue;
        private readonly ILogger<CleanupQueue> _logger;
        private int _queueDepth = 0;

        public CleanupQueue(ILogger<CleanupQueue> logger)
        {
            _logger = logger;

            var options = new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            };

            _queue = Channel.CreateUnbounded<StuckUploadJob>(options);
        }

        public async ValueTask EnqueueAsync(StuckUploadJob job, CancellationToken cancellationToken = default)
        {
            await _queue.Writer.WriteAsync(job, cancellationToken);
            Interlocked.Increment(ref _queueDepth);

            _logger.LogInformation(
                "Enqueued stuck upload cleanup job: AttachmentId={AttachmentId}, FileKey={FileKey}, Queue depth={Depth}",
                job.AttachmentId, job.FileKey, _queueDepth);
        }

        public async ValueTask<StuckUploadJob> DequeueAsync(CancellationToken cancellationToken = default)
        {
            var job = await _queue.Reader.ReadAsync(cancellationToken);
            Interlocked.Decrement(ref _queueDepth);

            _logger.LogInformation(
                "Dequeued stuck upload cleanup job: AttachmentId={AttachmentId}, Remaining in queue={Depth}",
                job.AttachmentId, _queueDepth);

            return job;
        }

        public int GetQueueDepth() => _queueDepth;

        public void Dispose()
        {
            _queue.Writer.Complete();
        }
    }
}
