using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IAnalyticsRepository
    {
        // User Activity Metrics
        Task<UserActivityMetrics?> GetUserActivityByDateAsync(Guid userId, DateOnly date);
        Task<List<UserActivityMetrics>> GetUserActivityRangeAsync(Guid userId, DateOnly startDate, DateOnly endDate);
        Task UpsertUserActivityAsync(UserActivityMetrics metrics);

        // User Productivity Scores
        Task<UserProductivityScores?> GetUserProductivityAsync(Guid userId, Guid? groupId, DateOnly weekStart);
        Task<List<UserProductivityScores>> GetUserProductivityRangeAsync(Guid userId, DateOnly startWeek, DateOnly endWeek);
        Task UpsertUserProductivityAsync(UserProductivityScores score);

        // Group Analytics
        Task<GroupAnalytics?> GetGroupAnalyticsByDateAsync(Guid groupId, DateOnly date);
        Task<List<GroupAnalytics>> GetGroupAnalyticsRangeAsync(Guid groupId, DateOnly startDate, DateOnly endDate);
        Task<List<GroupAnalytics>> GetAllGroupAnalyticsRangeAsync(DateOnly startDate, DateOnly endDate);
        Task UpsertGroupAnalyticsAsync(GroupAnalytics analytics);

        // Studio Analytics
        Task<StudioAnalytics?> GetStudioAnalyticsByDateAsync(Guid studioId, DateOnly date);
        Task<List<StudioAnalytics>> GetStudioAnalyticsRangeAsync(Guid studioId, DateOnly startDate, DateOnly endDate);
        Task<List<StudioAnalytics>> GetAllStudioAnalyticsRangeAsync(DateOnly startDate, DateOnly endDate);
        Task UpsertStudioAnalyticsAsync(StudioAnalytics analytics);

        // Task Performance Metrics
        Task<TaskPerformanceMetrics?> GetTaskPerformanceAsync(Guid taskId);
        Task<List<TaskPerformanceMetrics>> GetTaskPerformanceRangeAsync(Guid? userId, Guid? groupId, DateOnly startDate, DateOnly endDate);
        Task UpsertTaskPerformanceAsync(TaskPerformanceMetrics metrics);

        // Aggregation helpers for ETL jobs
        Task<Dictionary<Guid, int>> AggregateTasksCreatedByUserAsync(DateTime from, DateTime to);
        Task<Dictionary<Guid, int>> AggregateTasksCompletedByUserAsync(DateTime from, DateTime to);
        Task<Dictionary<Guid, int>> AggregateCommentsByUserAsync(DateTime from, DateTime to);
        Task<Dictionary<Guid, int>> AggregateMessagesByUserAsync(DateTime from, DateTime to);

        Task<Dictionary<Guid, int>> AggregateTasksByGroupAsync(Guid groupId, DateTime from, DateTime to);
        Task<Dictionary<Guid, int>> AggregateCompletedTasksByGroupAsync(Guid groupId, DateTime from, DateTime to);
        Task<Dictionary<Guid, int>> AggregateOverdueTasksByGroupAsync(Guid groupId, DateTime from, DateTime to);
        Task<Dictionary<Guid, int>> AggregateActiveMembersByGroupAsync(Guid groupId, DateTime from, DateTime to);
        Task<Dictionary<Guid, int>> AggregateMessagesByGroupAsync(Guid groupId, DateTime from, DateTime to);
        Task<Dictionary<Guid, int>> AggregateCommentsByGroupAsync(Guid groupId, DateTime from, DateTime to);
    }
}
