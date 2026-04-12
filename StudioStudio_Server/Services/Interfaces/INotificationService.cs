using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Services.Interfaces
{
    /// <summary>
    /// Service contract for task and mention notification delivery.
    /// Validate: user entities must be preloaded and represent non-deleted accounts.
    /// Returns: completed notification dispatch task for in-app + email channels.
    /// </summary>
    public interface INotificationService
    {
        Task NotifyTaskAssignedAsync(User assignee, User assignedBy, Guid taskId, string taskTitle, DateTime? deadline, CancellationToken cancellationToken = default);
        Task NotifyTaskReassignedAsync(User newAssignee, User oldAssignee, User actor, Guid taskId, string taskTitle, CancellationToken cancellationToken = default);
        Task NotifyTaskStatusChangedAsync(User user, User actor, Guid taskId, string oldStatus, string newStatus, string changedBy, CancellationToken cancellationToken = default);
        Task NotifyTaskCompletedAsync(User assignee, User completedBy, Guid taskId, string taskTitle, CancellationToken cancellationToken = default);
        Task NotifyMentionedInCommentAsync(User mentionedUser, User mentioner, Guid taskId, string taskTitle, string commentPreview, CancellationToken cancellationToken = default);
        Task NotifyMentionedInGroupDiscussAsync(User mentionedUser, User mentioner, Guid groupId, string groupName, string messagePreview, CancellationToken cancellationToken = default);
        Task NotifyTaskDeletedAsync(User assignee, User deletedBy, Guid taskId, string taskTitle, CancellationToken cancellationToken = default);
        Task NotifyTaskUnassignedAsync(User assignee, User actor, Guid taskId, string taskTitle, CancellationToken cancellationToken = default);
        Task NotifyTaskOverdueAsync(User assignee, Guid taskId, string taskTitle, DateTime dueDate, int overdueDays, CancellationToken cancellationToken = default);
        Task NotifyTaskReminderAsync(User assignee, Guid taskId, string taskTitle, DateTime dueDate, int hoursUntilDeadline, CancellationToken cancellationToken = default);
    }
}
