using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    public class AdminStatisticsService : IAdminStatisticsService
    {
        private readonly IAdminStatisticsRepository _repository;
        private readonly ILogger<AdminStatisticsService> _logger;

        public AdminStatisticsService(
            IAdminStatisticsRepository repository,
            ILogger<AdminStatisticsService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public async Task<HourlyActivityResponse> GetHourlyActivityAsync(HourlyActivityRequest request)
        {
            try
            {
                var startDate = request.StartDate ?? DateTime.MinValue;
                var endDate = request.EndDate ?? DateTime.UtcNow;

                // Get user login activity data grouped by hour and day of week (excluding admin accounts)
                // Data is obtained from RefreshToken creation time (when user logs in)
                var hourlyData = await _repository.GetHourlyLoginActivityAsync(startDate, endDate);

                var hourlyActivityPoints = hourlyData
                    .Select(x => new HourlyActivityDataPoint
                    {
                        Hour = x.Hour,
                        DayOfWeek = x.DayOfWeek,
                        DayName = GetDayName(x.DayOfWeek),
                        UserCount = x.Count
                    })
                    .ToList();

                return new HourlyActivityResponse
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    Data = hourlyActivityPoints
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting hourly activity");
                throw;
            }
        }

        public async Task<ReportStatusResponse> GetReportStatusAsync(ReportStatusRequest request)
        {
            try
            {
                var startDate = request.StartDate ?? DateTime.MinValue;
                var endDate = request.EndDate ?? DateTime.UtcNow;

                // Get all reports in date range (excluding reports from admin accounts)
                var reports = await _repository.GetReportsAsync(startDate, endDate);

                // Group by period
                var groupedReports = reports
                    .GroupBy(r => new
                    {
                        Year = r.CreatedAt.Year,
                        Month = r.CreatedAt.Month,
                        Date = new DateTime(r.CreatedAt.Year, r.CreatedAt.Month, 1)
                    })
                    .Select(g => new ReportStatusDataPoint
                    {
                        Date = g.Key.Date,
                        Period = $"T{g.Key.Month}",
                        Pending = g.Count(r => r.Status == ReportStatus.Open),
                        Processing = g.Count(r => r.Status == ReportStatus.InProgress),
                        Resolved = g.Count(r => r.Status == ReportStatus.Resolved),
                        Rejected = g.Count(r => r.Status == ReportStatus.Closed)
                    })
                    .OrderBy(d => d.Date)
                    .ToList();

                return new ReportStatusResponse
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    PeriodType = request.Period,
                    Data = groupedReports,
                    TotalReports = reports.Count
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting report status");
                throw;
            }
        }

        public async Task<UserDistributionResponse> GetUserDistributionAsync(UserDistributionRequest request)
        {
            try
            {
                var startDate = request.StartDate ?? DateTime.MinValue;
                var endDate = request.EndDate ?? DateTime.UtcNow;

                // Query users created in date range (excluding admin accounts)
                var users = await _repository.GetUsersAsync(startDate, endDate);

                var activeCount = users.Count(u => u.Status == UserStatus.Active);
                var inactiveCount = users.Count(u => u.Status == UserStatus.Inactive);
                var totalCount = users.Count;

                var distribution = new List<UserDistributionItem>
                {
                    new UserDistributionItem
                    {
                        Status = UserStatus.Active.ToString(),
                        Count = activeCount,
                        Percentage = totalCount > 0 ? Math.Round((decimal)activeCount / totalCount * 100, 2) : 0
                    },
                    new UserDistributionItem
                    {
                        Status = UserStatus.Inactive.ToString(),
                        Count = inactiveCount,
                        Percentage = totalCount > 0 ? Math.Round((decimal)inactiveCount / totalCount * 100, 2) : 0
                    }
                };

                return new UserDistributionResponse
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    TotalUsers = totalCount,
                    Distribution = distribution
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user distribution");
                throw;
            }
        }

        public async Task<SubscriptionDistributionResponse> GetSubscriptionDistributionAsync(SubscriptionDistributionRequest request)
        {
            try
            {
                var startDate = request.StartDate ?? DateTime.MinValue;
                var endDate = request.EndDate ?? DateTime.UtcNow;

                // Get all subscriptions (excluding subscriptions from admin accounts)
                var subscriptions = await _repository.GetSubscriptionsAsync(startDate, endDate);

                var users = await _repository.GetUsersAsync(startDate, endDate);
                var activeCount = users.Count(u => u.Status == UserStatus.Active);

                var premiumCount = subscriptions.Count(s => s.BillingCycle == BillingCycle.Monthly.ToString());
                var freeCount = activeCount - premiumCount;
                var totalCount = activeCount;

                // Calculate revenue for each type
                var freeRevenue = subscriptions
                    .Where(s => s.BillingCycle == BillingCycle.Free.ToString())
                    .Sum(s => s.Price);

                var premiumRevenue = subscriptions
                    .Where(s => s.BillingCycle == BillingCycle.Monthly.ToString())
                    .Sum(s => s.Price);

                var distribution = new List<SubscriptionDistributionItem>
                {
                    new SubscriptionDistributionItem
                    {
                        PlanType = "Free",
                        Count = freeCount,
                        Percentage = totalCount > 0 ? Math.Round((decimal)freeCount / totalCount * 100, 2) : 0,
                        TotalRevenue = freeRevenue
                    },
                    new SubscriptionDistributionItem
                    {
                        PlanType = "Premium",
                        Count = premiumCount,
                        Percentage = totalCount > 0 ? Math.Round((decimal)premiumCount / totalCount * 100, 2) : 0,
                        TotalRevenue = premiumRevenue
                    }
                };

                return new SubscriptionDistributionResponse
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    TotalSubscriptions = totalCount,
                    Distribution = distribution
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting subscription distribution");
                throw;
            }
        }

        public async Task<RecentActivityResponse> GetRecentActivityAsync(RecentActivityRequest request)
        {
            try
            {
                var startDate = request.StartDate ?? DateTime.MinValue;
                var endDate = request.EndDate ?? DateTime.UtcNow;
                var itemCount = request.ItemCount;

                var activities = new List<RecentActivityItem>();
                var activityId = 1;

                // New user signups (excluding admin accounts)
                var newUserCount = await _repository.CountRecentUserSignupsAsync(startDate, endDate);

                if (newUserCount > 0)
                {
                    var lastUser = await _repository.GetMostRecentUserSignupAsync(startDate, endDate);

                    if (lastUser != null)
                    {
                        activities.Add(new RecentActivityItem
                        {
                            Id = activityId++,
                            Type = "user_signup",
                            Title = "Người dùng mới đăng ký",
                            Message = $"{lastUser.Email}",
                            Count = newUserCount,
                            Timestamp = lastUser.CreatedAt
                        });
                    }
                }

                // Report submissions (excluding admin accounts)
                var reportCount = await _repository.CountRecentReportsAsync(startDate, endDate);

                if (reportCount > 0)
                {
                    var lastReport = await _repository.GetMostRecentReportAsync(startDate, endDate);

                    if (lastReport != null)
                    {
                        activities.Add(new RecentActivityItem
                        {
                            Id = activityId++,
                            Type = "report_submitted",
                            Title = "Báo cáo mới",
                            Message = lastReport.Title,
                            Count = reportCount,
                            Timestamp = lastReport.CreatedAt
                        });
                    }
                }

                // Premium upgrades (excluding admin accounts)
                var premiumUpgradeCount = await _repository.CountRecentPremiumUpgradesAsync(startDate, endDate);

                if (premiumUpgradeCount > 0)
                {
                    var lastUpgrade = await _repository.GetMostRecentPremiumUpgradeAsync(startDate, endDate);

                    if (lastUpgrade.HasValue && lastUpgrade.Value.Plan != null)
                    {
                        activities.Add(new RecentActivityItem
                        {
                            Id = activityId++,
                            Type = "premium_upgrade",
                            Title = "Nâng cấp Premium",
                            Message = lastUpgrade.Value.Plan.PlanName,
                            Count = premiumUpgradeCount,
                            Timestamp = lastUpgrade.Value.StartDate
                        });
                    }
                }

                // Group creations (excluding groups created by admins)
                var groupCreateCount = await _repository.CountRecentGroupCreationsAsync(startDate, endDate);

                if (groupCreateCount > 0)
                {
                    var lastGroup = await _repository.GetMostRecentGroupCreationAsync(startDate, endDate);

                    if (lastGroup != null)
                    {
                        activities.Add(new RecentActivityItem
                        {
                            Id = activityId++,
                            Type = "group_created",
                            Title = "Tạo nhóm mới",
                            Message = lastGroup.GroupName,
                            Count = groupCreateCount,
                            Timestamp = lastGroup.CreatedAt
                        });
                    }
                }

                // Sort by timestamp descending and take top items
                var recentActivities = activities
                    .OrderByDescending(a => a.Timestamp)
                    .Take(itemCount)
                    .ToList();

                return new RecentActivityResponse
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    Activities = recentActivities
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent activity");
                throw;
            }
        }

        public async Task<TopActiveGroupsResponse> GetTopActiveGroupsAsync(TopActiveGroupsRequest request)
        {
            try
            {
                var startDate = request.StartDate ?? DateTime.MinValue;
                var endDate = request.EndDate ?? DateTime.UtcNow;
                var topCount = request.TopCount;

                var topGroups = await _repository.GetTopActiveGroupsAsync(startDate, endDate, topCount);

                var result = topGroups
                    .Select((item) => new TopActiveGroupItem
                    {
                        GroupId = item.Group.GroupId,
                        GroupName = item.Group.GroupName,
                        MemberCount = item.MemberCount,
                        TotalTasks = item.TotalTasks,
                        CompletedTasks = item.CompletedTasks,
                        CompletionRate = item.TotalTasks > 0
                            ? Math.Round((decimal)item.CompletedTasks / item.TotalTasks * 100, 2)
                            : 0,
                        LastActivityAt = item.Group.UpdatedAt
                    })
                    .ToList();

                return new TopActiveGroupsResponse
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    Groups = result
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting top active groups");
                throw;
            }
        }

        private static string GetDayName(int dayOfWeek)
        {
            return dayOfWeek switch
            {
                0 => "CN",
                1 => "T2",
                2 => "T3",
                3 => "T4",
                4 => "T5",
                5 => "T6",
                6 => "T7",
                _ => ""
            };
        }
    }
}
