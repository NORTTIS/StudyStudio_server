using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IAdminStatisticsRepository
    {
        /// <summary>
        /// Get hourly login activity data grouped by hour and day of week
        /// Returns login events from RefreshToken creation (when user logs in)
        /// Excludes admin accounts
        /// </summary>
        Task<List<(int Hour, int DayOfWeek, int Count)>> GetHourlyLoginActivityAsync(
            DateTime startDate,
            DateTime endDate);

        /// <summary>
        /// Get all reports in date range excluding admin accounts
        /// </summary>
        Task<List<Report>> GetReportsAsync(
            DateTime startDate,
            DateTime endDate);

        /// <summary>
        /// Get all users excluding admin accounts in date range
        /// </summary>
        Task<List<User>> GetUsersAsync(
            DateTime startDate,
            DateTime endDate);

        /// <summary>
        /// Get all subscriptions excluding admin accounts in date range
        /// </summary>
        Task<List<(Guid SubscriptionId, Guid UserId, string PlanName, decimal Price, string BillingCycle, DateTime StartDate)>> GetSubscriptionsAsync(
            DateTime startDate,
            DateTime endDate);

        /// <summary>
        /// Get recent user signups excluding admin accounts
        /// </summary>
        Task<int> CountRecentUserSignupsAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// Get most recent user signup
        /// </summary>
        Task<User?> GetMostRecentUserSignupAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// Get recent reports excluding admin accounts
        /// </summary>
        Task<int> CountRecentReportsAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// Get most recent report
        /// </summary>
        Task<Report?> GetMostRecentReportAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// Get recent premium upgrades excluding admin accounts
        /// </summary>
        Task<int> CountRecentPremiumUpgradesAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// Get most recent premium upgrade
        /// </summary>
        Task<(User? User, SubscriptionPlan? Plan, DateTime StartDate)?> GetMostRecentPremiumUpgradeAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// Get recent group creations excluding groups created by admins
        /// </summary>
        Task<int> CountRecentGroupCreationsAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// Get most recent group creation
        /// </summary>
        Task<Group?> GetMostRecentGroupCreationAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// Get top active groups excluding those created by admins
        /// </summary>
        Task<List<(Group Group, int MemberCount, int TotalTasks, int CompletedTasks)>> GetTopActiveGroupsAsync(
            DateTime startDate,
            DateTime endDate,
            int topCount);
    }
}
