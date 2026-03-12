using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Models.DTOs.Response
{
    #region Revenue Overview

    /// <summary>
    /// Response DTO for revenue overview endpoint
    /// </summary>
    public class RevenueOverviewResponse
    {
        public decimal TotalRevenue { get; set; }
        public decimal MonthlyRevenue { get; set; }
        public decimal YearlyRevenue { get; set; }
        public int TotalTransactions { get; set; }
        public int SuccessfulTransactions { get; set; }
        public int FailedTransactions { get; set; }
        public decimal SuccessRate { get; set; }
        public int ActiveSubscriptions { get; set; }
        public decimal ARPU { get; set; }
        public decimal MRR { get; set; }
    }

    #endregion

    #region Revenue By Period

    /// <summary>
    /// Data point for revenue by period breakdown
    /// </summary>
    public class RevenueDataPoint
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
        public int TransactionCount { get; set; }
        public int NewSubscriptions { get; set; }
        public int Renewals { get; set; }
    }

    /// <summary>
    /// Response DTO for revenue by period endpoint
    /// </summary>
    public class RevenueByPeriodResponse
    {
        public string Period { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TransactionCount { get; set; }
        public decimal AverageOrderValue { get; set; }
        public List<RevenueDataPoint> Breakdown { get; set; } = new();
    }

    #endregion

    #region Revenue By Plan

    /// <summary>
    /// Plan revenue summary
    /// </summary>
    public class PlanRevenueSummary
    {
        public Guid PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string BillingCycle { get; set; } = string.Empty;
        public decimal TotalRevenue { get; set; }
        public int TransactionCount { get; set; }
        public int ActiveSubscriptions { get; set; }
        public decimal Percentage { get; set; }
        public string Trend { get; set; } = "stable"; // "up" | "down" | "stable"
    }

    /// <summary>
    /// Response DTO for revenue by plan endpoint
    /// </summary>
    public class RevenueByPlanResponse
    {
        public List<PlanRevenueSummary> Plans { get; set; } = new();
    }

    #endregion

    #region Revenue Trends

    /// <summary>
    /// Trend data for a specific period
    /// </summary>
    public class TrendData
    {
        public string Period { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TransactionCount { get; set; }
        public int NewCustomers { get; set; }
        public int ChurnedCustomers { get; set; }
        public decimal AverageOrderValue { get; set; }
    }

    /// <summary>
    /// Response DTO for revenue trends endpoint
    /// </summary>
    public class RevenueTrendsResponse
    {
        public TrendData CurrentPeriod { get; set; } = new();
        public TrendData? PreviousPeriod { get; set; }
        public decimal GrowthRate { get; set; }
        public string TrendDirection { get; set; } = "stable"; // "up" | "down" | "stable"
    }

    #endregion

    #region Top Plans

    /// <summary>
    /// Top plan item
    /// </summary>
    public class TopPlanItem
    {
        public int Rank { get; set; }
        public Guid PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal TotalRevenue { get; set; }
        public int ActiveSubscriptions { get; set; }
        public int NewSubscriptions { get; set; }
        public decimal ConversionRate { get; set; }
    }

    /// <summary>
    /// Response DTO for top plans endpoint
    /// </summary>
    public class TopPlansResponse
    {
        public List<TopPlanItem> TopPlans { get; set; } = new();
    }

    #endregion

    #region Revenue Transactions

    /// <summary>
    /// Transaction detail for revenue transactions list
    /// </summary>
    public class TransactionDetail
    {
        public Guid PaymentId { get; set; }
        public long OrderCode { get; set; }
        public Guid UserId { get; set; }
        public string UserEmail { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public Guid PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public PaymentStatus PaymentStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
    }

    /// <summary>
    /// Response DTO for revenue transactions endpoint
    /// </summary>
    public class RevenueTransactionsResponse
    {
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
        public List<TransactionDetail> Transactions { get; set; } = new();
    }

    #endregion

    #region MRR Breakdown

    /// <summary>
    /// MRR data for a specific month
    /// </summary>
    public class MRRMonthData
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public decimal StartMRR { get; set; }
        public decimal NewMRR { get; set; }
        public decimal ExpansionMRR { get; set; }
        public decimal ChurnMRR { get; set; }
        public decimal ContractionMRR { get; set; }
        public decimal EndMRR { get; set; }
        public decimal NetMRR { get; set; }
    }

    /// <summary>
    /// Response DTO for MRR breakdown endpoint
    /// </summary>
    public class MRRBreakdownResponse
    {
        public decimal CurrentMRR { get; set; }
        public List<MRRMonthData> MonthlyBreakdown { get; set; } = new();
    }

    #endregion

    #region Export Response

    /// <summary>
    /// Response DTO for export endpoint
    /// </summary>
    public class RevenueExportResponse
    {
        public string FileName { get; set; } = string.Empty;
        public byte[] FileContent { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    }

    #endregion
}
