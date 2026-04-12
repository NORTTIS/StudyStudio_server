using System.Threading.Channels;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services.TaskNotificationQueue
{
    public class TaskUpdateNotificationQueue : ITaskUpdateNotificationQueue
    {
        private readonly Channel<TaskUpdateNotificationJob> _queue;
        private readonly ILogger<TaskUpdateNotificationQueue> _logger;
        private int _queueDepth;

        public TaskUpdateNotificationQueue(ILogger<TaskUpdateNotificationQueue> logger)
        {
            _logger = logger;
            _queue = Channel.CreateUnbounded<TaskUpdateNotificationJob>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
        }

        public async ValueTask EnqueueAsync(TaskUpdateNotificationJob job, CancellationToken cancellationToken = default)
        {
            var depth = Interlocked.Increment(ref _queueDepth);

            try
            {
                await _queue.Writer.WriteAsync(job, cancellationToken);
            }
            catch
            {
                Interlocked.Decrement(ref _queueDepth);
                throw;
            }

            _logger.LogInformation(
                "Enqueued task update notification job: TaskId={TaskId}, GroupId={GroupId}, QueueDepth={QueueDepth}",
                job.TaskId, job.GroupId, depth);
        }

        public async ValueTask<TaskUpdateNotificationJob> DequeueAsync(CancellationToken cancellationToken = default)
        {
            var job = await _queue.Reader.ReadAsync(cancellationToken);
            Interlocked.Decrement(ref _queueDepth);
            return job;
        }

        public int GetQueueDepth() => _queueDepth;
    }
}