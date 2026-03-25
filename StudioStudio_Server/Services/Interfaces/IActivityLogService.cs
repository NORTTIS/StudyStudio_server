using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IActivityLogService
    {
        /// <summary>
        /// Log a general user action with context
        /// </summary>
        Task LogAsync(ActivityLog log);

        /// <summary>
        /// Get task delete activity logs by task IDs
        /// </summary>
        Task<List<ActivityLog>> GetTaskDeleteLogsAsync(List<Guid> taskIds);

        /// <summary>
        /// Log task creation activity with priority/severity for weighted contribution scoring
        /// </summary>
        Task LogTaskCreateAsync(Guid userId, Guid taskId, Guid? groupId, Guid? studioId,
            int priority = 0, int severity = 0);

        /// <summary>
        /// Log task update activity
        /// </summary>
        Task LogTaskUpdateAsync(Guid userId, Guid taskId, Guid? groupId, Guid? studioId,
            int priority = 0, int severity = 0, string? field = null, string? oldValue = null, string? newValue = null);

        /// <summary>
        /// Log task completion activity with priority/severity for weighted contribution scoring
        /// </summary>
        Task LogTaskCompleteAsync(Guid userId, Guid taskId, Guid? groupId,
            int priority = 0, int severity = 0);

        /// <summary>
        /// Log task assignment activity
        /// </summary>
        Task LogTaskAssignAsync(Guid userId, Guid taskId, Guid assignedTo, Guid? groupId);

        /// <summary>
        /// Log comment creation activity
        /// </summary>
        Task LogCommentCreateAsync(Guid userId, Guid commentId, Guid taskId, Guid? groupId);

        /// <summary>
        /// Log message creation activity
        /// </summary>
        Task LogMessageCreateAsync(Guid userId, Guid messageId, Guid groupId, Guid? studioId);

        /// <summary>
        /// Log group creation activity
        /// </summary>
        Task LogGroupCreateAsync(Guid userId, Guid groupId, Guid studioId);

        /// <summary>
        /// Log group join activity
        /// </summary>
        Task LogGroupJoinAsync(Guid userId, Guid groupId, Guid studioId);

        /// <summary>
        /// Log task delete activity
        /// </summary>
        Task LogTaskDeleteAsync(Guid userId, Guid taskId, Guid? groupId,
            int priority = 0, int severity = 0);
    }
}
