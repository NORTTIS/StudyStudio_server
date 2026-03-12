using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Services.Interfaces;
using PaymentStatusEnum = StudioStudio_Server.Models.Enums.PaymentStatus;

namespace StudioStudio_Server.Services
{
    public class RevenueService : IRevenueService
    {
        private readonly StudioDbContext _db;
        private readonly ILogger<RevenueService> _logger;

        public RevenueService(
            StudioDbContext db,
            ILogger<RevenueService> logger)
        {
            _db = db;
            _logger = logger;
        }

        #region Revenue Overview

        public async Task<RevenueOverviewResponse> GetRevenueOverviewAsync()
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var startOfYear = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // Total revenue (all time) - only SUCCESS payments
            var totalRevenue = await _db.Payments
                .Where(p => p.PaymentStatus == PaymentStatusEnum.SUCCESS)
                .SumAsync(p => p.Amount);

            // Monthly revenue
            var monthlyRevenue = await _db.Payments
                .Where(p => p.PaymentStatus == PaymentStatusEnum.SUCCESS && p.CreatedAt >= startOfMonth)
                .SumAsync(p => p.Amount);

            // Yearly revenue
            var yearlyRevenue = await _db.Payments
                .Where(p => p.PaymentStatus == PaymentStatusEnum.SUCCESS && p.CreatedAt >= startOfYear)
                .SumAsync(p => p.Amount);

            // Total transactions
            var totalTransactions = await _db.Payments.CountAsync();

            // Successful transactions
            var successfulTransactions = await _db.Payments
                .CountAsync(p => p.PaymentStatus == PaymentStatusEnum.SUCCESS);

            // Failed transactions (CANCELLED or FAILED)
            var failedTransactions = await _db.Payments
                .CountAsync(p => p.PaymentStatus == PaymentStatusEnum.CANCELLED || p.PaymentStatus == PaymentStatusEnum.FAILED);

            // Success rate
            var successRate = totalTransactions > 0
                ? Math.Round((decimal)successfulTransactions / totalTransactions * 100, 2)
                : 0;

            // Active subscriptions
            var activeSubscriptions = await _db.UserSubscriptions
                .CountAsync(u => u.IsActive && u.EndDate > now);

            // ARPU (Average Revenue Per User) - unique paying users
            var payingUserCount = await _db.Payments
                .Where(p => p.PaymentStatus == PaymentStatusEnum.SUCCESS)
                .Select(p => p.UserId)
                .Distinct()
                .CountAsync();

            var arpu = payingUserCount > 0
                ? Math.Round(totalRevenue / payingUserCount, 2)
                : 0;

            // MRR (Monthly Recurring Revenue) - active monthly subscriptions
            var mrr = await _db.UserSubscriptions
                .Include(u => u.Plan)
                .Where(u => u.IsActive && u.EndDate > now && u.Plan.BillingCycle == BillingCycle.Monthly)
                .SumAsync(u => u.Plan.Price);

            return new RevenueOverviewResponse
            {
                TotalRevenue = totalRevenue,
                MonthlyRevenue = monthlyRevenue,
                YearlyRevenue = yearlyRevenue,
                TotalTransactions = totalTransactions,
                SuccessfulTransactions = successfulTransactions,
                FailedTransactions = failedTransactions,
                SuccessRate = successRate,
                ActiveSubscriptions = activeSubscriptions,
                ARPU = arpu,
                MRR = mrr
            };
        }

        #endregion

        #region Revenue By Period

        public async Task<RevenueByPeriodResponse> GetRevenueByPeriodAsync(RevenueByPeriodRequest request)
        {
            // Convert to UTC to ensure compatibility with PostgreSQL timestamp with time zone
            var startDate = request.StartDate.ToUniversalTime();
            var endDate = request.EndDate.ToUniversalTime();

            var query = _db.Payments
                .Include(p => p.User)
                .Include(p => p.Plan)
                .Where(p => p.PaymentStatus == PaymentStatusEnum.SUCCESS
                    && p.CreatedAt >= startDate
                    && p.CreatedAt <= endDate);

            if (request.PlanId.HasValue)
            {
                query = query.Where(p => p.PlanId == request.PlanId.Value);
            }

            var payments = await query.OrderBy(p => p.CreatedAt).ToListAsync();

            // Group by period
            var breakdown = request.Period.ToLower() switch
            {
                "daily" => GroupByDay(payments, startDate, endDate),
                "weekly" => GroupByWeek(payments, startDate, endDate),
                "monthly" => GroupByMonth(payments, startDate, endDate),
                "yearly" => GroupByYear(payments, startDate, endDate),
                _ => GroupByDay(payments, startDate, endDate)
            };

            var totalRevenue = breakdown.Sum(b => b.Revenue);
            var transactionCount = breakdown.Sum(b => b.TransactionCount);
            var averageOrderValue = transactionCount > 0
                ? Math.Round(totalRevenue / transactionCount, 2)
                : 0;

            return new RevenueByPeriodResponse
            {
                Period = request.Period,
                StartDate = startDate,
                EndDate = endDate,
                TotalRevenue = totalRevenue,
                TransactionCount = transactionCount,
                AverageOrderValue = averageOrderValue,
                Breakdown = breakdown
            };
        }

        private List<RevenueDataPoint> GroupByDay(List<Payment> payments, DateTime startDate, DateTime endDate)
        {
            var result = new List<RevenueDataPoint>();
            var currentDate = startDate.Date;

            while (currentDate <= endDate.Date)
            {
                var dayPayments = payments.Where(p => p.CreatedAt.Date == currentDate).ToList();
                var newSubscriptions = dayPayments.Count(p => p.CreatedAt.Date == p.PaidAt?.Date);

                result.Add(new RevenueDataPoint
                {
                    Date = currentDate,
                    Revenue = dayPayments.Sum(p => p.Amount),
                    TransactionCount = dayPayments.Count,
                    NewSubscriptions = newSubscriptions,
                    Renewals = dayPayments.Count - newSubscriptions
                });

                currentDate = currentDate.AddDays(1);
            }

            return result;
        }

        private List<RevenueDataPoint> GroupByWeek(List<Payment> payments, DateTime startDate, DateTime endDate)
        {
            var result = new List<RevenueDataPoint>();
            var currentDate = startDate.Date;

            while (currentDate <= endDate.Date)
            {
                var weekEnd = currentDate.AddDays(6);
                if (weekEnd > endDate.Date) weekEnd = endDate.Date;

                var weekPayments = payments.Where(p => p.CreatedAt.Date >= currentDate && p.CreatedAt.Date <= weekEnd).ToList();
                var newSubscriptions = weekPayments.Count(p => p.CreatedAt.Date == p.PaidAt?.Date);

                result.Add(new RevenueDataPoint
                {
                    Date = currentDate,
                    Revenue = weekPayments.Sum(p => p.Amount),
                    TransactionCount = weekPayments.Count,
                    NewSubscriptions = newSubscriptions,
                    Renewals = weekPayments.Count - newSubscriptions
                });

                currentDate = currentDate.AddDays(7);
            }

            return result;
        }

        private List<RevenueDataPoint> GroupByMonth(List<Payment> payments, DateTime startDate, DateTime endDate)
        {
            var result = new List<RevenueDataPoint>();
            var currentDate = new DateTime(startDate.Year, startDate.Month, 1);

            while (currentDate <= endDate.Date)
            {
                var monthEnd = currentDate.AddMonths(1).AddDays(-1);
                if (monthEnd > endDate.Date) monthEnd = endDate.Date;

                var monthPayments = payments.Where(p => p.CreatedAt.Date >= currentDate && p.CreatedAt.Date <= monthEnd).ToList();
                var newSubscriptions = monthPayments.Count(p => p.CreatedAt.Date == p.PaidAt?.Date);

                result.Add(new RevenueDataPoint
                {
                    Date = currentDate,
                    Revenue = monthPayments.Sum(p => p.Amount),
                    TransactionCount = monthPayments.Count,
                    NewSubscriptions = newSubscriptions,
                    Renewals = monthPayments.Count - newSubscriptions
                });

                currentDate = currentDate.AddMonths(1);
            }

            return result;
        }

        private List<RevenueDataPoint> GroupByYear(List<Payment> payments, DateTime startDate, DateTime endDate)
        {
            var result = new List<RevenueDataPoint>();
            var currentYear = startDate.Year;

            while (currentYear <= endDate.Year)
            {
                var yearStart = new DateTime(currentYear, 1, 1);
                var yearEnd = new DateTime(currentYear, 12, 31);
                if (yearStart < startDate) yearStart = startDate;
                if (yearEnd > endDate) yearEnd = endDate;

                var yearPayments = payments.Where(p => p.CreatedAt.Date >= yearStart && p.CreatedAt.Date <= yearEnd).ToList();
                var newSubscriptions = yearPayments.Count(p => p.CreatedAt.Date == p.PaidAt?.Date);

                result.Add(new RevenueDataPoint
                {
                    Date = yearStart,
                    Revenue = yearPayments.Sum(p => p.Amount),
                    TransactionCount = yearPayments.Count,
                    NewSubscriptions = newSubscriptions,
                    Renewals = yearPayments.Count - newSubscriptions
                });

                currentYear++;
            }

            return result;
        }

        #endregion

        #region Revenue By Plan

        public async Task<RevenueByPlanResponse> GetRevenueByPlanAsync(RevenueByPlanRequest request)
        {
            // Convert to UTC to ensure compatibility with PostgreSQL timestamp with time zone
            var startDate = (request.StartDate ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1)).ToUniversalTime();
            var endDate = (request.EndDate ?? DateTime.UtcNow).ToUniversalTime();
            var now = DateTime.UtcNow;

            var plans = await _db.SubscriptionPlans.ToListAsync();
            var planSummaries = new List<PlanRevenueSummary>();

            // Get total revenue in period for percentage calculation
            var totalRevenueInPeriod = await _db.Payments
                .Where(p => p.PaymentStatus == PaymentStatusEnum.SUCCESS
                    && p.CreatedAt >= startDate
                    && p.CreatedAt <= endDate)
                .SumAsync(p => p.Amount);

            foreach (var plan in plans)
            {
                // Revenue for this plan in period
                var planRevenue = await _db.Payments
                    .Where(p => p.PaymentStatus == PaymentStatusEnum.SUCCESS
                        && p.PlanId == plan.PlanId
                        && p.CreatedAt >= startDate
                        && p.CreatedAt <= endDate)
                    .SumAsync(p => p.Amount);

                // Transaction count
                var transactionCount = await _db.Payments
                    .CountAsync(p => p.PaymentStatus == PaymentStatusEnum.SUCCESS
                        && p.PlanId == plan.PlanId
                        && p.CreatedAt >= startDate
                        && p.CreatedAt <= endDate);

                // Active subscriptions
                var activeSubscriptions = await _db.UserSubscriptions
                    .CountAsync(u => u.PlanId == plan.PlanId && u.IsActive && u.EndDate > now);

                // Calculate percentage
                var percentage = totalRevenueInPeriod > 0
                    ? Math.Round(planRevenue / totalRevenueInPeriod * 100, 2)
                    : 0;

                // Calculate trend (compare to previous period)
                var previousPeriodStart = startDate.AddDays(-(endDate - startDate).Days);
                var previousPeriodRevenue = await _db.Payments
                    .Where(p => p.PaymentStatus == PaymentStatusEnum.SUCCESS
                        && p.PlanId == plan.PlanId
                        && p.CreatedAt >= previousPeriodStart
                        && p.CreatedAt < startDate)
                    .SumAsync(p => p.Amount);

                var trend = "stable";
                if (previousPeriodRevenue > 0)
                {
                    var changePercent = (planRevenue - previousPeriodRevenue) / previousPeriodRevenue * 100;
                    if (changePercent > 5) trend = "up";
                    else if (changePercent < -5) trend = "down";
                }
                else if (planRevenue > 0)
                {
                    trend = "up";
                }

                planSummaries.Add(new PlanRevenueSummary
                {
                    PlanId = plan.PlanId,
                    PlanName = plan.PlanName,
                    Price = plan.Price,
                    BillingCycle = plan.BillingCycle.ToString(),
                    TotalRevenue = planRevenue,
                    TransactionCount = transactionCount,
                    ActiveSubscriptions = activeSubscriptions,
                    Percentage = percentage,
                    Trend = trend
                });
            }

            return new RevenueByPlanResponse
            {
                Plans = planSummaries.OrderByDescending(p => p.TotalRevenue).ToList()
            };
        }

        #endregion

        #region Revenue Trends

        public async Task<RevenueTrendsResponse> GetRevenueTrendsAsync(RevenueTrendsRequest request)
        {
            var (currentStart, currentEnd) = GetPeriodDates(request.Period, request.StartDate, request.EndDate);
            var previousStart = currentStart.AddDays(-(currentEnd - currentStart).Days);
            var previousEnd = currentStart.AddDays(-1);

            // Current period data
            var currentRevenue = await _db.Payments
                .Where(p => p.PaymentStatus == PaymentStatusEnum.SUCCESS
                    && p.CreatedAt >= currentStart
                    && p.CreatedAt <= currentEnd)
                .SumAsync(p => p.Amount);

            var currentTransactions = await _db.Payments
                .CountAsync(p => p.PaymentStatus == PaymentStatusEnum.SUCCESS
                    && p.CreatedAt >= currentStart
                    && p.CreatedAt <= currentEnd);

            var newCustomers = await _db.UserSubscriptions
                .CountAsync(u => u.StartDate >= currentStart && u.StartDate <= currentEnd);

            // Simplified churn calculation - users whose subscriptions ended in the period
            var churnedCustomers = await _db.UserSubscriptions
                .CountAsync(u => u.EndDate >= currentStart && u.EndDate <= currentEnd && u.IsActive == false);

            var currentAOV = currentTransactions > 0
                ? Math.Round(currentRevenue / currentTransactions, 2)
                : 0;

            var currentPeriodData = new TrendData
            {
                Period = request.Period,
                StartDate = currentStart,
                EndDate = currentEnd,
                TotalRevenue = currentRevenue,
                TransactionCount = currentTransactions,
                NewCustomers = newCustomers,
                ChurnedCustomers = churnedCustomers,
                AverageOrderValue = currentAOV
            };

            // Previous period data (if comparison requested)
            TrendData? previousPeriodData = null;
            if (request.Comparison)
            {
                var previousRevenue = await _db.Payments
                    .Where(p => p.PaymentStatus == PaymentStatusEnum.SUCCESS
                        && p.CreatedAt >= previousStart
                        && p.CreatedAt <= previousEnd)
                    .SumAsync(p => p.Amount);

                var previousTransactions = await _db.Payments
                    .CountAsync(p => p.PaymentStatus == PaymentStatusEnum.SUCCESS
                        && p.CreatedAt >= previousStart
                        && p.CreatedAt <= previousEnd);

                var previousAOV = previousTransactions > 0
                    ? Math.Round(previousRevenue / previousTransactions, 2)
                    : 0;

                previousPeriodData = new TrendData
                {
                    Period = "previous",
                    StartDate = previousStart,
                    EndDate = previousEnd,
                    TotalRevenue = previousRevenue,
                    TransactionCount = previousTransactions,
                    NewCustomers = 0, // Would need additional logic
                    ChurnedCustomers = 0,
                    AverageOrderValue = previousAOV
                };
            }

            // Calculate growth rate
            var growthRate = 0m;
            var trendDirection = "stable";
            if (previousPeriodData != null && previousPeriodData.TotalRevenue > 0)
            {
                growthRate = Math.Round((currentRevenue - previousPeriodData.TotalRevenue) / previousPeriodData.TotalRevenue * 100, 2);
                if (growthRate > 0) trendDirection = "up";
                else if (growthRate < 0) trendDirection = "down";
            }
            else if (currentRevenue > 0)
            {
                growthRate = 100;
                trendDirection = "up";
            }

            return new RevenueTrendsResponse
            {
                CurrentPeriod = currentPeriodData,
                PreviousPeriod = previousPeriodData,
                GrowthRate = growthRate,
                TrendDirection = trendDirection
            };
        }

        private (DateTime start, DateTime end) GetPeriodDates(string period, DateTime? customStart, DateTime? customEnd)
        {
            var now = DateTime.UtcNow;
            // Convert custom dates to UTC if provided
            var start = customStart?.ToUniversalTime() ?? now.AddDays(-30);
            var end = customEnd?.ToUniversalTime() ?? now;

            return period.ToLower() switch
            {
                "last7days" => (now.AddDays(-6).Date, now.Date),
                "last30days" => (now.AddDays(-29).Date, now.Date),
                "last90days" => (now.AddDays(-89).Date, now.Date),
                "last12months" => (now.AddMonths(-11).Date, now.Date),
                "custom" => (start, end),
                _ => (now.AddDays(-29).Date, now.Date)
            };
        }

        #endregion

        #region Top Plans

        public async Task<TopPlansResponse> GetTopPlansAsync(TopPlansRequest request)
        {
            var limit = Math.Min(request.Limit, 10);
            // Convert to UTC to ensure compatibility with PostgreSQL timestamp with time zone
            var startDate = (request.StartDate ?? DateTime.UtcNow.AddMonths(-1)).ToUniversalTime();
            var endDate = (request.EndDate ?? DateTime.UtcNow).ToUniversalTime();

            var plans = await _db.SubscriptionPlans.ToListAsync();
            var now = DateTime.UtcNow;

            var topPlans = new List<TopPlanItem>();
            var rank = 1;

            foreach (var plan in plans)
            {
                // Revenue in period
                var totalRevenue = await _db.Payments
                    .Where(p => p.PaymentStatus == PaymentStatusEnum.SUCCESS
                        && p.PlanId == plan.PlanId
                        && p.CreatedAt >= startDate
                        && p.CreatedAt <= endDate)
                    .SumAsync(p => p.Amount);

                // Active subscriptions
                var activeSubscriptions = await _db.UserSubscriptions
                    .CountAsync(u => u.PlanId == plan.PlanId && u.IsActive && u.EndDate > now);

                // New subscriptions in period
                var newSubscriptions = await _db.UserSubscriptions
                    .CountAsync(u => u.PlanId == plan.PlanId
                        && u.StartDate >= startDate
                        && u.StartDate <= endDate);

                // Total paid users for this plan (for conversion rate)
                var totalPaidUsers = await _db.Payments
                    .Where(p => p.PaymentStatus == PaymentStatusEnum.SUCCESS && p.PlanId == plan.PlanId)
                    .Select(p => p.UserId)
                    .Distinct()
                    .CountAsync();

                var totalUsers = await _db.Users.CountAsync();
                var conversionRate = totalUsers > 0
                    ? Math.Round((decimal)totalPaidUsers / totalUsers * 100, 2)
                    : 0;

                topPlans.Add(new TopPlanItem
                {
                    Rank = rank++,
                    PlanId = plan.PlanId,
                    PlanName = plan.PlanName,
                    Price = plan.Price,
                    TotalRevenue = totalRevenue,
                    ActiveSubscriptions = activeSubscriptions,
                    NewSubscriptions = newSubscriptions,
                    ConversionRate = conversionRate
                });
            }

            // Sort by the specified criteria
            var sortedPlans = request.SortBy.ToLower() switch
            {
                "revenue" => topPlans.OrderByDescending(p => p.TotalRevenue).ToList(),
                "subscriptions" => topPlans.OrderByDescending(p => p.ActiveSubscriptions).ToList(),
                "growth" => topPlans.OrderByDescending(p => p.NewSubscriptions).ToList(),
                _ => topPlans.OrderByDescending(p => p.TotalRevenue).ToList()
            };

            // Re-assign ranks after sorting
            for (int i = 0; i < sortedPlans.Count; i++)
            {
                sortedPlans[i].Rank = i + 1;
            }

            return new TopPlansResponse
            {
                TopPlans = sortedPlans.Take(limit).ToList()
            };
        }

        #endregion

        #region Revenue Transactions

        public async Task<RevenueTransactionsResponse> GetRevenueTransactionsAsync(RevenueTransactionsRequest request)
        {
            var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;
            var pageSize = Math.Min(request.PageSize > 0 ? request.PageSize : 20, 100);

            var query = _db.Payments
                .Include(p => p.User)
                .Include(p => p.Plan)
                .AsQueryable();

            // Apply filters - convert to UTC to ensure compatibility with PostgreSQL timestamp with time zone
            if (request.StartDate.HasValue)
            {
                query = query.Where(p => p.CreatedAt >= request.StartDate.Value.ToUniversalTime());
            }

            if (request.EndDate.HasValue)
            {
                query = query.Where(p => p.CreatedAt <= request.EndDate.Value.ToUniversalTime());
            }

            if (request.PlanId.HasValue)
            {
                query = query.Where(p => p.PlanId == request.PlanId.Value);
            }

            if (!string.IsNullOrEmpty(request.PaymentStatus) && Enum.TryParse<PaymentStatusEnum>(request.PaymentStatus, true, out var status))
            {
                query = query.Where(p => p.PaymentStatus == status);
            }

            if (!string.IsNullOrEmpty(request.SearchTerm))
            {
                var searchTerm = request.SearchTerm.ToLower();
                query = query.Where(p =>
                    p.User.Email.ToLower().Contains(searchTerm) ||
                    (p.User.FirstName + " " + p.User.LastName).ToLower().Contains(searchTerm) ||
                    p.OrderCode.ToString().Contains(searchTerm));
            }

            // Get total count before pagination
            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            // Apply pagination and ordering
            var transactions = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new TransactionDetail
                {
                    PaymentId = p.PaymentId,
                    OrderCode = p.OrderCode,
                    UserId = p.UserId,
                    UserEmail = p.User.Email,
                    UserName = p.User.FirstName + " " + p.User.LastName,
                    PlanId = p.PlanId,
                    PlanName = p.Plan.PlanName,
                    Amount = p.Amount,
                    PaymentMethod = p.PaymentMethod,
                    PaymentStatus = p.PaymentStatus,
                    CreatedAt = p.CreatedAt,
                    PaidAt = p.PaidAt
                })
                .ToListAsync();

            return new RevenueTransactionsResponse
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                Transactions = transactions
            };
        }

        #endregion

        #region MRR Breakdown

        public async Task<MRRBreakdownResponse> GetMRRBreakdownAsync(MRRRequest request)
        {
            var year = request.Year ?? DateTime.UtcNow.Year;
            var now = DateTime.UtcNow;

            var monthlyData = new List<MRRMonthData>();

            for (int month = 1; month <= 12; month++)
            {
                if (month > now.Month && year == now.Year) break;

                var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);

                // Start MRR - subscriptions active at start of month
                var startMRR = await _db.UserSubscriptions
                    .Include(u => u.Plan)
                    .Where(u => u.IsActive
                        && u.StartDate <= monthStart
                        && u.EndDate > monthStart
                        && u.Plan.BillingCycle == BillingCycle.Monthly)
                    .SumAsync(u => u.Plan.Price);

                // End MRR - subscriptions active at end of month
                var endMRR = await _db.UserSubscriptions
                    .Include(u => u.Plan)
                    .Where(u => u.IsActive
                        && u.StartDate <= monthEnd
                        && u.EndDate > monthEnd
                        && u.Plan.BillingCycle == BillingCycle.Monthly)
                    .SumAsync(u => u.Plan.Price);

                // New MRR - new subscriptions in this month
                var newSubscriptions = await _db.UserSubscriptions
                    .Include(u => u.Plan)
                    .Where(u => u.StartDate >= monthStart
                        && u.StartDate <= monthEnd
                        && u.Plan.BillingCycle == BillingCycle.Monthly)
                    .ToListAsync();

                var newMRR = newSubscriptions.Where(u => u.IsActive).Sum(u => u.Plan.Price);

                // Expansion MRR - upgrades (simplified)
                var expansionMRR = 0m;

                // Churn MRR - subscriptions that ended in this month
                var churnedSubscriptions = await _db.UserSubscriptions
                    .Include(u => u.Plan)
                    .Where(u => u.EndDate >= monthStart
                        && u.EndDate <= monthEnd
                        && u.Plan.BillingCycle == BillingCycle.Monthly)
                    .ToListAsync();

                var churnMRR = churnedSubscriptions.Sum(u => u.Plan.Price);

                // Contraction MRR - downgrades (simplified)
                var contractionMRR = 0m;

                var netMRR = endMRR - startMRR;

                monthlyData.Add(new MRRMonthData
                {
                    Month = month,
                    Year = year,
                    StartMRR = startMRR,
                    NewMRR = newMRR,
                    ExpansionMRR = expansionMRR,
                    ChurnMRR = churnMRR,
                    ContractionMRR = contractionMRR,
                    EndMRR = endMRR,
                    NetMRR = netMRR
                });
            }

            var currentMRR = monthlyData.LastOrDefault()?.EndMRR ?? 0;

            return new MRRBreakdownResponse
            {
                CurrentMRR = currentMRR,
                MonthlyBreakdown = monthlyData
            };
        }

        #endregion

        #region Export Revenue Report

        public async Task<RevenueExportResponse> ExportRevenueReportAsync(RevenueExportRequest request)
        {
            // For now, return a placeholder - in production, use ClosedXML
            var fileName = $"revenue_report_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";

            // Get data based on report type
            byte[] fileContent;
            switch (request.ReportType.ToLower())
            {
                case "overview":
                    var overview = await GetRevenueOverviewAsync();
                    fileContent = await GenerateOverviewExcel(overview);
                    break;
                case "by-period":
                    var periodRequest = new RevenueByPeriodRequest
                    {
                        StartDate = request.StartDate ?? DateTime.UtcNow.AddMonths(-1),
                        EndDate = request.EndDate ?? DateTime.UtcNow,
                        Period = request.Period ?? "daily"
                    };
                    var byPeriod = await GetRevenueByPeriodAsync(periodRequest);
                    fileContent = await GenerateByPeriodExcel(byPeriod);
                    break;
                case "by-plan":
                    var planRequest = new RevenueByPlanRequest
                    {
                        StartDate = request.StartDate,
                        EndDate = request.EndDate
                    };
                    var byPlan = await GetRevenueByPlanAsync(planRequest);
                    fileContent = await GenerateByPlanExcel(byPlan);
                    break;
                case "transactions":
                    var transRequest = new RevenueTransactionsRequest
                    {
                        StartDate = request.StartDate,
                        EndDate = request.EndDate,
                        PageNumber = 1,
                        PageSize = 1000
                    };
                    var transactions = await GetRevenueTransactionsAsync(transRequest);
                    fileContent = await GenerateTransactionsExcel(transactions);
                    break;
                default:
                    var defaultOverview = await GetRevenueOverviewAsync();
                    fileContent = await GenerateOverviewExcel(defaultOverview);
                    break;
            }

            return new RevenueExportResponse
            {
                FileName = fileName,
                FileContent = fileContent,
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            };
        }

        private Task<byte[]> GenerateOverviewExcel(RevenueOverviewResponse overview)
        {
            // Placeholder - would use ClosedXML in production
            // For now, return empty byte array
            return Task.FromResult(Array.Empty<byte>());
        }

        private Task<byte[]> GenerateByPeriodExcel(RevenueByPeriodResponse data)
        {
            return Task.FromResult(Array.Empty<byte>());
        }

        private Task<byte[]> GenerateByPlanExcel(RevenueByPlanResponse data)
        {
            return Task.FromResult(Array.Empty<byte>());
        }

        private Task<byte[]> GenerateTransactionsExcel(RevenueTransactionsResponse data)
        {
            return Task.FromResult(Array.Empty<byte>());
        }

        #endregion
    }
}
