using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IAnalyticsRepository
    {
        // Group Analytics
        Task<GroupAnalytics?> GetGroupAnalyticsByDateAsync(Guid groupId, DateOnly date);
        Task<List<GroupAnalytics>> GetGroupAnalyticsRangeAsync(Guid groupId, DateOnly startDate, DateOnly endDate);
        Task<List<GroupAnalytics>> GetGroupAnalyticsRangeAsync(StudioDbContext context, Guid groupId, DateOnly startDate, DateOnly endDate);
        Task<List<GroupAnalytics>> GetAllGroupAnalyticsRangeAsync(DateOnly startDate, DateOnly endDate);
        Task<Dictionary<Guid, List<GroupAnalytics>>> GetGroupAnalyticsRangeBatchAsync(List<Guid> groupIds, DateOnly startDate, DateOnly endDate);
        Task UpsertGroupAnalyticsAsync(GroupAnalytics analytics);

        // Aggregation helpers for ETL jobs
        Task<Dictionary<Guid, int>> AggregateTasksByGroupAsync(Guid groupId, DateTime from, DateTime to);
        Task<Dictionary<Guid, int>> AggregateCompletedTasksByGroupAsync(Guid groupId, DateTime from, DateTime to);
        Task<Dictionary<Guid, int>> AggregateOverdueTasksByGroupAsync(Guid groupId, DateTime from, DateTime to);
        Task<Dictionary<Guid, int>> AggregateActiveMembersByGroupAsync(Guid groupId, DateTime from, DateTime to);
        Task<Dictionary<Guid, int>> AggregateMessagesByGroupAsync(Guid groupId, DateTime from, DateTime to);
        Task<Dictionary<Guid, int>> AggregateCommentsByGroupAsync(Guid groupId, DateTime from, DateTime to);

        // === GROUP ANALYTICS ENHANCED: Task status per member ===
        /// <summary>
        /// Get task status counts (done, in-progress, todo, overdue) per member in a group within date range
        /// </summary>
        Task<Dictionary<Guid, (int Done, int InProgress, int Todo, int Overdue, int InProgressOverdue, int TodoOverdue, int Total)>> GetMemberTaskStatusBreakdownAsync(
            Guid groupId, DateTime from, DateTime to);
        Task<Dictionary<Guid, (int Done, int InProgress, int Todo, int Overdue, int InProgressOverdue, int TodoOverdue, int Total)>> GetMemberTaskStatusBreakdownAsync(
            StudioDbContext context, Guid groupId, DateTime from, DateTime to);

        /// <summary>
        /// Get daily completed tasks count per member within date range — for line chart
        /// </summary>
        Task<Dictionary<Guid, Dictionary<DateOnly, int>>> GetMemberDailyCompletionsAsync(
            Guid groupId, DateOnly startDate, DateOnly endDate);
        Task<Dictionary<Guid, Dictionary<DateOnly, int>>> GetMemberDailyCompletionsAsync(
            StudioDbContext context, Guid groupId, DateOnly startDate, DateOnly endDate);

        /// <summary>
        /// Get last activity datetime per member in a group
        /// </summary>
        Task<Dictionary<Guid, DateTime?>> GetMemberLastActivityAsync(Guid groupId);
        Task<Dictionary<Guid, DateTime?>> GetMemberLastActivityAsync(StudioDbContext context, Guid groupId);

        /// <summary>
        /// Get task status counts per member WITHOUT date filter (all time) - for summary endpoint
        /// </summary>
        Task<Dictionary<Guid, (int Done, int InProgress, int Todo, int Overdue, int InProgressOverdue, int TodoOverdue, int Total)>> GetMemberTaskStatusBreakdownAllTimeAsync(Guid groupId);

        // ==================== PERSONAL ANALYTICS ====================

        /// <summary>
        /// Get all personal tasks (GroupId = null) for a user
        /// </summary>
        Task<List<(Guid? GroupId, int Progress, DateTime? CompletedAt, DateTime? DueDate, int Priority)>> GetUserPersonalTasksAsync(Guid userId);

        /// <summary>
        /// Get all tasks in all groups user belongs to
        /// </summary>
        Task<List<(Guid GroupId, Guid OwnerId, string GroupName, int Progress, DateTime? CompletedAt, DateTime? DueDate, int Priority)>> GetUserGroupsTasksAsync(List<Guid> groupIds);

        /// <summary>
        /// Get user's personal tasks completion times (from CreatedAt to CompletedAt)
        /// </summary>
        Task<List<double>> GetUserPersonalTaskCompletionTimesAsync(Guid userId);

        /// <summary>
        /// Get total activity score per group for a user across all their groups (for contribution rate)
        /// Activity = tasksCompleted:10.0 * pw * sw + tasksCreated:3 + tasksUpdated:1 + commentsCreated:1 + messagesSent:1 +CommentTask:1
        /// </summary>
        Task<Dictionary<Guid, double>> GetUserGroupActivityScoresAsync(List<Guid> groupIds, Guid userId, DateTime? from = null, DateTime? to = null);

        /// <summary>
        /// Get activity scores for ALL members across the given groups (all time).
        /// Returns: Dictionary&lt;GroupId, Dictionary&lt;UserId, TotalScore&gt;&gt;
        /// Used by GetUserGroupRankingsAsync to compute each user's contribution % within each group.
        /// </summary>
        Task<Dictionary<Guid, Dictionary<Guid, double>>> GetAllMembersGroupActivityScoresAsync(
            List<Guid> groupIds, DateTime? from = null, DateTime? to = null);

        /// <summary>
        /// Get per-member total scores and messages for a SINGLE group (all time).
        /// Returns Dictionary&lt;UserId, MemberContributionResult&gt;.
        /// Used by GetGroupMemberContributionAsync (GroupSummary) to ensure unified scoring.
        /// Scoring: ActivityScoreHelper with assignee credit for TASK_COMPLETE, messages from GroupMessages.
        /// </summary>
        Task<Dictionary<Guid, MemberContributionResult>> GetGroupMemberScoresAsync(Guid groupId);

        /// <summary>
        /// Get user's tasks grouped by priority with status breakdown (all time, personal + all groups)
        /// </summary>
        Task<List<(int Priority, int Done, int InProgress, int Overdue, int Todo, int Total)>> GetUserTasksByPriorityAsync(
            List<Guid> groupIds, Guid userId);

        /// <summary>
        /// Get user's overdue tasks across all groups + personal
        /// </summary>
        Task<List<(Guid? GroupId, Guid? TaskId, string Title, string GroupName, DateTime DueDate)>> GetUserOverdueTasksAsync(
            List<Guid> groupIds, Guid userId, int limit = 10);

        /// <summary>
        /// Get user's tasks due within N days across all groups + personal
        /// </summary>
        Task<List<(Guid? GroupId, Guid? TaskId, string Title, string GroupName, DateTime DueDate)>> GetUserDueSoonTasksAsync(
            List<Guid> groupIds, Guid userId, int days = 1, int limit = 10);

        /// <summary>
        /// Get user's stuck tasks (no status update in N days)
        /// </summary>
        Task<List<(Guid? GroupId, Guid? TaskId, string Title, string GroupName, DateTime LastUpdated)>> GetUserStuckTasksAsync(
            List<Guid> groupIds, Guid userId, int noUpdateDays = 5, int limit = 10);

        /// <summary>
        /// Get user's weekly completion stats (last N weeks) for benchmark
        /// </summary>
        Task<List<(int Year, int Week, int Score, int Count)>> GetUserWeeklyScoresAsync(
            List<Guid> groupIds, Guid userId, int weeks = 7);

        /// <summary>
        /// Get group's average weekly score for benchmark comparison
        /// </summary>
        Task<double?> GetGroupAvgWeeklyScoreAsync(Guid groupId, int year, int week);
    }
}
