using StudioStudio_Server.Services.TaskNotificationQueue;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface ITaskUpdateNotificationQueue
    {
        ValueTask EnqueueAsync(TaskUpdateNotificationJob job, CancellationToken cancellationToken = default);
        ValueTask<TaskUpdateNotificationJob> DequeueAsync(CancellationToken cancellationToken = default);
        int GetQueueDepth();
    }
}