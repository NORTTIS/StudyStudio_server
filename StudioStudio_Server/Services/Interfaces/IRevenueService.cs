using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IRevenueService
    {
        /// <summary>
        /// Get revenue overview with key metrics
        /// </summary>
        Task<RevenueOverviewResponse> GetRevenueOverviewAsync();

        /// <summary>
        /// Get revenue breakdown by time period
        /// </summary>
        Task<RevenueByPeriodResponse> GetRevenueByPeriodAsync(RevenueByPeriodRequest request);

        /// <summary>
        /// Get revenue breakdown by subscription plan
        /// </summary>
        Task<RevenueByPlanResponse> GetRevenueByPlanAsync(RevenueByPlanRequest request);

        /// <summary>
        /// Get revenue trends with optional comparison to previous period
        /// </summary>
        Task<RevenueTrendsResponse> GetRevenueTrendsAsync(RevenueTrendsRequest request);

        /// <summary>
        /// Get top performing subscription plans
        /// </summary>
        Task<TopPlansResponse> GetTopPlansAsync(TopPlansRequest request);

        /// <summary>
        /// Get paginated revenue transactions
        /// </summary>
        Task<RevenueTransactionsResponse> GetRevenueTransactionsAsync(RevenueTransactionsRequest request);

        /// <summary>
        /// Get MRR breakdown by month for a specific year
        /// </summary>
        Task<MRRBreakdownResponse> GetMRRBreakdownAsync(MRRRequest request);

        /// <summary>
        /// Export revenue report to Excel file
        /// </summary>
        Task<RevenueExportResponse> ExportRevenueReportAsync(RevenueExportRequest request);
    }
}
