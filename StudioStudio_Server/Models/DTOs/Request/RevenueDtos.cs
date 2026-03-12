namespace StudioStudio_Server.Models.DTOs.Request
{
    #region Revenue By Period Request

    /// <summary>
    /// Request DTO for revenue by period endpoint
    /// </summary>
    public class RevenueByPeriodRequest
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Period { get; set; } = "daily"; // "daily" | "weekly" | "monthly" | "yearly"
        public Guid? PlanId { get; set; }
    }

    #endregion

    #region Revenue Trends Request

    /// <summary>
    /// Request DTO for revenue trends endpoint
    /// </summary>
    public class RevenueTrendsRequest
    {
        public string Period { get; set; } = "last30days"; // "last7days" | "last30days" | "last90days" | "last12months" | "custom"
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public bool Comparison { get; set; } = false;
    }

    #endregion

    #region Revenue Transactions Request

    /// <summary>
    /// Request DTO for revenue transactions endpoint
    /// </summary>
    public class RevenueTransactionsRequest
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public Guid? PlanId { get; set; }
        public string? PaymentStatus { get; set; }
        public string? SubscriptionStatus { get; set; } // "active" | "expired" | "all"
        public string? SearchTerm { get; set; }
    }

    #endregion

    #region Revenue Export Request

    /// <summary>
    /// Request DTO for revenue export endpoint
    /// </summary>
    public class RevenueExportRequest
    {
        public string ReportType { get; set; } = "overview"; // "overview" | "by-period" | "by-plan" | "transactions"
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Period { get; set; } // "daily" | "weekly" | "monthly" | "yearly"
        public bool IncludeCharts { get; set; } = false;
    }

    #endregion

    #region Revenue By Plan Request

    /// <summary>
    /// Request DTO for revenue by plan endpoint
    /// </summary>
    public class RevenueByPlanRequest
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    #endregion

    #region Top Plans Request

    /// <summary>
    /// Request DTO for top plans endpoint
    /// </summary>
    public class TopPlansRequest
    {
        public int Limit { get; set; } = 5;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string SortBy { get; set; } = "revenue"; // "revenue" | "subscriptions" | "growth"
    }

    #endregion

    #region MRR Request

    /// <summary>
    /// Request DTO for MRR breakdown endpoint
    /// </summary>
    public class MRRRequest
    {
        public int? Year { get; set; }
    }

    #endregion
}
