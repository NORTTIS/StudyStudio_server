using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IAnalyticsService
    {
        // User Dashboard
        Task<UserDashboardResponse> GetUserDashboardAsync(Guid userId, DateOnly? startDate, DateOnly? endDate);
        Task<List<ActivityHeatmapData>> GetUserActivityHeatmapAsync(Guid userId, int days = 30);
        Task<List<TaskCompletionTrendData>> GetTaskCompletionTrendAsync(Guid userId, int days = 30);
        Task<DeadlinePerformanceData> GetDeadlinePerformanceAsync(Guid userId);

        // Group Dashboard
        Task<GroupAnalyticsResponse> GetGroupAnalyticsAsync(Guid groupId, Guid userId, DateOnly? startDate, DateOnly? endDate);
        Task<List<MemberContributionData>> GetGroupMemberContributionAsync(Guid groupId);

        // Group Summary (no date filter) - for Chart 1,2,4,6
        Task<GroupSummaryResponse> GetGroupSummaryAsync(Guid groupId, Guid userId);

        // Group Analytics Enhanced (for GroupAnalyticPage)
        Task<List<MemberTaskBreakdownData>> GetMemberTaskBreakdownAsync(Guid groupId, DateOnly? startDate, DateOnly? endDate);
        Task<List<MemberProgressTrendData>> GetMemberProgressTrendAsync(Guid groupId, DateOnly? startDate, DateOnly? endDate);
        Task<List<MemberHeatmapData>> GetMemberHeatmapAsync(Guid groupId, DateOnly? startDate, DateOnly? endDate);
        Task<List<MemberActivitySummary>> GetMemberActivitySummaryAsync(Guid groupId, DateOnly? startDate, DateOnly? endDate);

        // Studio Dashboard
        Task<List<GroupComparisonData>> GetStudioGroupComparisonAsync(Guid studioId);
        Task<StudioGroupHeatmapResponse> GetStudioGroupHeatmapAsync(Guid studioId, DateOnly startDate, DateOnly endDate);

        // Studio Overview (Chart 1 & 2)
        Task<StudioOverviewResponse> GetStudioOverviewAsync(Guid studioId);

        // Studio Completion Trend (Chart 3)
        Task<StudioCompletionTrendResponse> GetStudioCompletionTrendAsync(
            Guid studioId,
            DateOnly? startDate,
            DateOnly? endDate,
            List<Guid>? groupIds);

        // Studio Group Status (Chart 4)
        Task<StudioGroupStatusResponse> GetStudioGroupStatusAsync(
            Guid studioId,
            DateOnly? startDate,
            DateOnly? endDate);

        // Studio Group Activity Heatmap (Chart 5)
        Task<StudioGroupActivityResponse> GetStudioGroupActivityAsync(
            Guid studioId,
            DateOnly? startDate,
            DateOnly? endDate);
    }
}
