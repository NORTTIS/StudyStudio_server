using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IAdminStatisticsService
    {
        /// <summary>
        /// Get hourly activity heatmap data
        /// </summary>
        Task<HourlyActivityResponse> GetHourlyActivityAsync(HourlyActivityRequest request);

        /// <summary>
        /// Get report status breakdown by period
        /// </summary>
        Task<ReportStatusResponse> GetReportStatusAsync(ReportStatusRequest request);

        /// <summary>
        /// Get user distribution (Active/Inactive)
        /// </summary>
        Task<UserDistributionResponse> GetUserDistributionAsync(UserDistributionRequest request);

        /// <summary>
        /// Get subscription distribution (Free/Premium)
        /// </summary>
        Task<SubscriptionDistributionResponse> GetSubscriptionDistributionAsync(SubscriptionDistributionRequest request);

        /// <summary>
        /// Get recent activity feed
        /// </summary>
        Task<RecentActivityResponse> GetRecentActivityAsync(RecentActivityRequest request);

        /// <summary>
        /// Get top active groups
        /// </summary>
        Task<TopActiveGroupsResponse> GetTopActiveGroupsAsync(TopActiveGroupsRequest request);
    }
}
