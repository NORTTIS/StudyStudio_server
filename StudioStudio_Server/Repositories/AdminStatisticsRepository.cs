using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository handling data access for admin statistics
    /// </summary>
    public class AdminStatisticsRepository : IAdminStatisticsRepository
    {
        private readonly StudioDbContext _context;

        public AdminStatisticsRepository(StudioDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get hourly login activity data grouped by hour and day of week
        /// Uses RefreshToken creation time to track login events
        /// Excludes admin accounts
        /// </summary>
        public async Task<List<(int Hour, int DayOfWeek, int Count)>> GetHourlyLoginActivityAsync(
            DateTime startDate,
            DateTime endDate)
        {
            var adminUserIds = await GetAdminUserIdsAsync();

            var hourlyData = await _context.RefreshToken
                .Include(rt => rt.User)
                .Where(rt => !adminUserIds.Contains(rt.UserId) &&
                             rt.User.CreatedAt >= startDate &&
                             rt.User.CreatedAt <= endDate)
                .GroupBy(rt => new
                {
                    Hour = rt.User.CreatedAt.Hour,
                    DayOfWeek = (int)rt.User.CreatedAt.DayOfWeek
                })
                .Select(g => new { g.Key.Hour, g.Key.DayOfWeek, Count = g.Count() })
                .AsNoTracking()
                .ToListAsync();

            return hourlyData
                .Select(x => (x.Hour, x.DayOfWeek, x.Count))
                .ToList();
        }

        /// <summary>
        /// Get all reports in date range excluding admin accounts
        /// </summary>
        public async Task<List<Report>> GetReportsAsync(
            DateTime startDate,
            DateTime endDate)
        {
            var adminUserIds = await GetAdminUserIdsAsync();

            return await _context.Reports
                .Where(r => !adminUserIds.Contains(r.UserId ?? Guid.Empty) &&
                            r.CreatedAt >= startDate &&
                            r.CreatedAt <= endDate)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Get all users excluding admin accounts in date range
        /// </summary>
        public async Task<List<User>> GetUsersAsync(
            DateTime startDate,
            DateTime endDate)
        {
            return await _context.Users
                .Where(u => !u.IsAdmin &&
                            u.CreatedAt >= startDate &&
                            u.CreatedAt <= endDate)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Get all subscriptions excluding admin accounts in date range
        /// </summary>
        public async Task<List<(Guid SubscriptionId, Guid UserId, string PlanName, decimal Price, string BillingCycle, DateTime StartDate)>> GetSubscriptionsAsync(
            DateTime startDate,
            DateTime endDate)
        {
            var adminUserIds = await GetAdminUserIdsAsync();

            var subscriptions = await _context.UserSubscriptions
                .Include(us => us.User)
                .Include(us => us.Plan)
                .Where(us => !adminUserIds.Contains(us.UserId) &&
                             us.StartDate >= startDate &&
                             us.StartDate <= endDate)
                .Select(us => new
                {
                    us.SubscriptionId,
                    us.UserId,
                    us.Plan.PlanName,
                    us.Plan.Price,
                    BillingCycle = us.Plan.BillingCycle.ToString(),
                    us.StartDate
                })
                .AsNoTracking()
                .ToListAsync();

            return subscriptions
                .Select(s => (s.SubscriptionId, s.UserId, s.PlanName, s.Price, s.BillingCycle, s.StartDate))
                .ToList();
        }

        /// <summary>
        /// Get recent user signups excluding admin accounts
        /// </summary>
        public async Task<int> CountRecentUserSignupsAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.Users
                .CountAsync(u => !u.IsAdmin &&
                                 u.CreatedAt >= startDate &&
                                 u.CreatedAt <= endDate);
        }

        /// <summary>
        /// Get most recent user signup
        /// </summary>
        public async Task<User?> GetMostRecentUserSignupAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.Users
                .Where(u => !u.IsAdmin &&
                            u.CreatedAt >= startDate &&
                            u.CreatedAt <= endDate)
                .OrderByDescending(u => u.CreatedAt)
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Get recent reports excluding admin accounts
        /// </summary>
        public async Task<int> CountRecentReportsAsync(DateTime startDate, DateTime endDate)
        {
            var adminUserIds = await GetAdminUserIdsAsync();

            return await _context.Reports
                .CountAsync(r => !adminUserIds.Contains(r.UserId ?? Guid.Empty) &&
                                 r.CreatedAt >= startDate &&
                                 r.CreatedAt <= endDate);
        }

        /// <summary>
        /// Get most recent report
        /// </summary>
        public async Task<Report?> GetMostRecentReportAsync(DateTime startDate, DateTime endDate)
        {
            var adminUserIds = await GetAdminUserIdsAsync();

            return await _context.Reports
                .Where(r => !adminUserIds.Contains(r.UserId ?? Guid.Empty) &&
                            r.CreatedAt >= startDate &&
                            r.CreatedAt <= endDate)
                .OrderByDescending(r => r.CreatedAt)
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Get recent premium upgrades excluding admin accounts
        /// </summary>
        public async Task<int> CountRecentPremiumUpgradesAsync(DateTime startDate, DateTime endDate)
        {
            var adminUserIds = await GetAdminUserIdsAsync();

            return await _context.UserSubscriptions
                .Include(us => us.Plan)
                .CountAsync(us => !adminUserIds.Contains(us.UserId) &&
                                  us.Plan.BillingCycle == BillingCycle.Monthly &&
                                  us.StartDate >= startDate &&
                                  us.StartDate <= endDate);
        }

        /// <summary>
        /// Get most recent premium upgrade
        /// </summary>
        public async Task<(User? User, SubscriptionPlan? Plan, DateTime StartDate)?> GetMostRecentPremiumUpgradeAsync(DateTime startDate, DateTime endDate)
        {
            var adminUserIds = await GetAdminUserIdsAsync();

            var upgrade = await _context.UserSubscriptions
                .Include(us => us.User)
                .Include(us => us.Plan)
                .Where(us => !adminUserIds.Contains(us.UserId) &&
                             us.Plan.BillingCycle == BillingCycle.Monthly &&
                             us.StartDate >= startDate &&
                             us.StartDate <= endDate)
                .OrderByDescending(us => us.StartDate)
                .Select(us => new { us.User, us.Plan, us.StartDate })
                .AsNoTracking()
                .FirstOrDefaultAsync();

            return upgrade != null ? (upgrade.User, upgrade.Plan, upgrade.StartDate) : null;
        }

        /// <summary>
        /// Get recent group creations excluding groups created by admins
        /// </summary>
        public async Task<int> CountRecentGroupCreationsAsync(DateTime startDate, DateTime endDate)
        {
            var adminUserIds = await GetAdminUserIdsAsync();

            return await _context.Groups
                .CountAsync(g => !adminUserIds.Contains(g.CreatedBy) &&
                                 g.CreatedAt >= startDate &&
                                 g.CreatedAt <= endDate);
        }

        /// <summary>
        /// Get most recent group creation
        /// </summary>
        public async Task<Group?> GetMostRecentGroupCreationAsync(DateTime startDate, DateTime endDate)
        {
            var adminUserIds = await GetAdminUserIdsAsync();

            return await _context.Groups
                .Where(g => !adminUserIds.Contains(g.CreatedBy) &&
                            g.CreatedAt >= startDate &&
                            g.CreatedAt <= endDate)
                .OrderByDescending(g => g.CreatedAt)
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Get top active groups excluding those created by admins
        /// </summary>
        public async Task<List<(Group Group, int MemberCount, int TotalTasks, int CompletedTasks)>> GetTopActiveGroupsAsync(
            DateTime startDate,
            DateTime endDate,
            int topCount)
        {
            var adminUserIds = await GetAdminUserIdsAsync();

            var topGroups = await _context.Groups
                .Include(g => g.Participants)
                .Where(g => !adminUserIds.Contains(g.CreatedBy) &&
                            g.CreatedAt >= startDate &&
                            g.CreatedAt <= endDate)
                .ToListAsync();

            var result = topGroups
                .Select(g => new
                {
                    Group = g,
                    MemberCount = g.Participants.Count,
                    TotalTasks = _context.Tasks.Count(t => t.GroupId == g.GroupId && !t.IsPendingDeleted),
                    CompletedTasks = _context.Tasks.Count(t => t.GroupId == g.GroupId && t.Progress >= 100 && !t.IsPendingDeleted)
                })
                .OrderByDescending(x => x.CompletedTasks)
                .ThenByDescending(x => x.MemberCount)
                .Take(topCount)
                .Select(x => (x.Group, x.MemberCount, x.TotalTasks, x.CompletedTasks))
                .ToList();

            return result;
        }

        /// <summary>
        /// Get admin user IDs
        /// </summary>
        public async Task<List<Guid>> GetAdminUserIdsAsync()
        {
            return await _context.Users
                .Where(u => u.IsAdmin)
                .Select(u => u.UserId)
                .ToListAsync();
        }
    }
}
