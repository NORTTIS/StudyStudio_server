using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IAnalyticsService
    {
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

        // ==================== PERSONAL ANALYTICS (AnalysisHome) ====================
        Task<UserKpiSummaryResponse> GetUserKpiSummaryAsync(Guid userId);
        Task<UserTaskStatusResponse> GetUserTaskStatusAsync(Guid userId);
        Task<UserGroupRankingsResponse> GetUserGroupRankingsAsync(Guid userId);
        Task<UserProductivityTrendResponse> GetUserProductivityTrendAsync(Guid userId, int periodDays = 30);
        Task<UserOnTimeOverviewResponse> GetUserOnTimeOverviewAsync(Guid userId);
        Task<UserPriorityDistributionResponse> GetUserPriorityDistributionAsync(Guid userId);
        Task<UserUrgencyDistributionResponse> GetUserUrgencyDistributionAsync(Guid userId);
        Task<UserBenchmarkResponse> GetUserBenchmarkAsync(Guid userId, int weeks = 7, Guid? groupId = null);
        Task<UserRiskAlertsResponse> GetUserRiskAlertsAsync(Guid userId, int limit = 10);
    }
}
