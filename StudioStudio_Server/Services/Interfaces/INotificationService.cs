using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface INotificationService
    {
        Task NotifyTaskAssignedAsync(User assignee, User assignedBy, Guid taskId, string taskTitle, DateTime? deadline);
        Task NotifyTaskReassignedAsync(User newAssignee, User oldAssignee, User actor, Guid taskId, string taskTitle);
        Task NotifyTaskStatusChangedAsync(User user, Guid taskId, string oldStatus, string newStatus, string changedBy);
        Task NotifyTaskCompletedAsync(User assignee, User completedBy, Guid taskId, string taskTitle);
        Task NotifyMentionedInCommentAsync(User mentionedUser, User mentioner, Guid taskId, string taskTitle, string commentPreview);
        Task NotifyMentionedInGroupDiscussAsync(User mentionedUser, User mentioner, Guid groupId, string groupName, string messagePreview);
        Task NotifyTaskDeletedAsync(User assignee, User deletedBy, Guid taskId, string taskTitle);
        Task NotifyTaskUnassignedAsync(User assignee, User actor, Guid taskId, string taskTitle);
        Task NotifyTaskOverdueAsync(User assignee, Guid taskId, string taskTitle, DateTime dueDate, int overdueDays);
        Task NotifyTaskReminderAsync(User assignee, Guid taskId, string taskTitle, DateTime dueDate, int hoursUntilDeadline);
    }
}
