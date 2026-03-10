namespace StudioStudio_Server.Models.DTOs.Response
{
    /// <summary>
    /// Paginated response for billing history
    /// </summary>
    public class BillingHistoryResponse
    {
        public List<BillingHistoryItem> Items { get; set; } = new();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    }

    /// <summary>
    /// Individual billing history item
    /// </summary>
    public class BillingHistoryItem
    {
        public Guid PaymentId { get; set; }
        public long OrderCode { get; set; }
        public Enums.PaymentStatus PaymentStatus { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }

        // User info
        public Guid UserId { get; set; }
        public string UserEmail { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;

        // Plan info
        public Guid PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
    }
}
