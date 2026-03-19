using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Services.Interfaces;
using System.Text.Json;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service for logging user activities
    /// </summary>
    public class ActivityLogService : IActivityLogService
    {
        private readonly StudioDbContext _context;

        public ActivityLogService(StudioDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Log a general user action with context
        /// </summary>
        public async Task LogAsync(ActivityLog log)
        {
            log.CreatedAt = DateTime.UtcNow;
            _context.ActivityLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Get task delete activity logs by task IDs
        /// </summary>
        public async Task<List<ActivityLog>> GetTaskDeleteLogsAsync(List<Guid> taskIds)
        {
            return await _context.ActivityLogs
                .Where(x => taskIds.Contains(x.TargetId!.Value) && x.ActionType == ActivityActionTypes.TASK_DELETE)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        /// <summary>
        /// Log task creation activity
        /// </summary>
        public async Task LogTaskCreateAsync(Guid userId, Guid taskId, Guid? groupId, Guid? studioId)
        {
            var log = new ActivityLog
            {
                LogId = Guid.NewGuid(),
                UserId = userId,
                ActionType = ActivityActionTypes.TASK_CREATE,
                TargetType = ActivityTargetTypes.TASK,
                TargetId = taskId,
                GroupId = groupId,
                StudioId = studioId,
                CreatedAt = DateTime.UtcNow
            };

            await LogAsync(log);
        }

        /// <summary>
        /// Log task update activity
        /// </summary>
        public async Task LogTaskUpdateAsync(Guid userId, Guid taskId, Guid? groupId, Guid? studioId, string field, string? oldValue, string? newValue)
        {
            var metadata = JsonSerializer.Serialize(new
            {
                Field = field,
                OldValue = oldValue,
                NewValue = newValue
            });

            var log = new ActivityLog
            {
                LogId = Guid.NewGuid(),
                UserId = userId,
                ActionType = ActivityActionTypes.TASK_UPDATE,
                TargetType = ActivityTargetTypes.TASK,
                TargetId = taskId,
                GroupId = groupId,
                StudioId = studioId,
                Metadata = metadata,
                CreatedAt = DateTime.UtcNow
            };

            await LogAsync(log);
        }

        /// <summary>
        /// Log task completion activity
        /// </summary>
        public async Task LogTaskCompleteAsync(Guid userId, Guid taskId, Guid? groupId)
        {
            var log = new ActivityLog
            {
                LogId = Guid.NewGuid(),
                UserId = userId,
                ActionType = ActivityActionTypes.TASK_COMPLETE,
                TargetType = ActivityTargetTypes.TASK,
                TargetId = taskId,
                GroupId = groupId,
                CreatedAt = DateTime.UtcNow
            };

            await LogAsync(log);
        }

        /// <summary>
        /// Log task assignment activity
        /// </summary>
        public async Task LogTaskAssignAsync(Guid userId, Guid taskId, Guid assignedTo, Guid? groupId)
        {
            var metadata = JsonSerializer.Serialize(new
            {
                AssignedTo = assignedTo
            });

            var log = new ActivityLog
            {
                LogId = Guid.NewGuid(),
                UserId = userId,
                ActionType = ActivityActionTypes.TASK_ASSIGN,
                TargetType = ActivityTargetTypes.TASK,
                TargetId = taskId,
                GroupId = groupId,
                Metadata = metadata,
                CreatedAt = DateTime.UtcNow
            };

            await LogAsync(log);
        }

        /// <summary>
        /// Log comment creation activity
        /// </summary>
        public async Task LogCommentCreateAsync(Guid userId, Guid commentId, Guid taskId, Guid? groupId)
        {
            var log = new ActivityLog
            {
                LogId = Guid.NewGuid(),
                UserId = userId,
                ActionType = ActivityActionTypes.COMMENT_CREATE,
                TargetType = ActivityTargetTypes.COMMENT,
                TargetId = commentId,
                GroupId = groupId,
                CreatedAt = DateTime.UtcNow
            };

            await LogAsync(log);
        }

        /// <summary>
        /// Log message creation activity
        /// </summary>
        public async Task LogMessageCreateAsync(Guid userId, Guid messageId, Guid groupId, Guid? studioId)
        {
            var log = new ActivityLog
            {
                LogId = Guid.NewGuid(),
                UserId = userId,
                ActionType = ActivityActionTypes.MESSAGE_CREATE,
                TargetType = ActivityTargetTypes.MESSAGE,
                TargetId = messageId,
                GroupId = groupId,
                StudioId = studioId,
                CreatedAt = DateTime.UtcNow
            };

            await LogAsync(log);
        }

        /// <summary>
        /// Log group creation activity
        /// </summary>
        public async Task LogGroupCreateAsync(Guid userId, Guid groupId, Guid studioId)
        {
            var log = new ActivityLog
            {
                LogId = Guid.NewGuid(),
                UserId = userId,
                ActionType = ActivityActionTypes.GROUP_CREATE,
                TargetType = ActivityTargetTypes.GROUP,
                TargetId = groupId,
                StudioId = studioId,
                CreatedAt = DateTime.UtcNow
            };

            await LogAsync(log);
        }

        /// <summary>
        /// Log group join activity
        /// </summary>
        public async Task LogGroupJoinAsync(Guid userId, Guid groupId, Guid studioId)
        {
            var log = new ActivityLog
            {
                LogId = Guid.NewGuid(),
                UserId = userId,
                ActionType = ActivityActionTypes.GROUP_JOIN,
                TargetType = ActivityTargetTypes.GROUP,
                TargetId = groupId,
                StudioId = studioId,
                CreatedAt = DateTime.UtcNow
            };

            await LogAsync(log);
        }

        /// <summary>
        /// Log task delete activity
        /// </summary>
        public async Task LogTaskDeleteAsync(Guid userId, Guid taskId, Guid? groupId)
        {
            var log = new ActivityLog
            {
                LogId = Guid.NewGuid(),
                UserId = userId,
                ActionType = ActivityActionTypes.TASK_DELETE,
                TargetType = ActivityTargetTypes.TASK,
                TargetId = taskId,
                GroupId = groupId,
                CreatedAt = DateTime.UtcNow
            };

            await LogAsync(log);
        }
    }

    /// <summary>
    /// Activity action type constants
    /// </summary>
    public static class ActivityActionTypes
    {
        public const string TASK_CREATE = "TASK_CREATE";
        public const string TASK_UPDATE = "TASK_UPDATE";
        public const string TASK_COMPLETE = "TASK_COMPLETE";
        public const string TASK_DELETE = "TASK_DELETE";
        public const string TASK_ASSIGN = "TASK_ASSIGN";
        public const string COMMENT_CREATE = "COMMENT_CREATE";
        public const string MESSAGE_CREATE = "MESSAGE_CREATE";
        public const string GROUP_CREATE = "GROUP_CREATE";
        public const string GROUP_JOIN = "GROUP_JOIN";
    }

    /// <summary>
    /// Activity target type constants
    /// </summary>
    public static class ActivityTargetTypes
    {
        public const string TASK = "TASK";
        public const string COMMENT = "COMMENT";
        public const string MESSAGE = "MESSAGE";
        public const string GROUP = "GROUP";
    }
}
