namespace StudioStudio_Server.Services.Interfaces
{
    public interface INotificationService
    {
        Task NotifyTaskAssignedAsync(Guid assigneeId, Guid taskId, Guid assignedBy, string taskTitle, DateTime? deadline);
        Task NotifyTaskReassignedAsync(Guid newAssigneeId, Guid oldAssigneeId, Guid taskId, Guid reassignedBy, string taskTitle);
        Task NotifyTaskStatusChangedAsync(Guid userId, Guid taskId, string oldStatus, string newStatus, string changedBy);
        Task NotifyTaskCompletedAsync(Guid assigneeId, Guid taskId, string taskTitle, Guid completedBy);
        Task NotifyMentionedInCommentAsync(Guid mentionedUserId, Guid taskId, string taskTitle, Guid mentionerId, string commentPreview);
        Task NotifyMentionedInGroupDiscussAsync(Guid mentionedUserId, Guid groupId, Guid mentionerId, string groupName, string messagePreview);
        Task NotifyTaskDeletedAsync(Guid assigneeId, Guid taskId, string taskTitle, Guid deletedBy);
        Task NotifyTaskUnassignedAsync(Guid previousAssigneeId, Guid taskId, string taskTitle, Guid unassignedBy);
        Task NotifyTaskOverdueAsync(Guid assigneeId, Guid taskId, string taskTitle, DateTime dueDate, int overdueDays);
        Task NotifyTaskReminderAsync(Guid assigneeId, Guid taskId, string taskTitle, DateTime dueDate, int hoursUntilDeadline);
    }
}
