namespace StudioStudio_Server.Models.Entities
{
    public class Payment
    {
        public Guid PaymentId { get; set; }

        public Guid UserId { get; set; }
        public Guid PlanId { get; set; }

        /// <summary>
        /// PayOS order code (numeric, max 9999999999999)
        /// Generated from current timestamp ticks
        /// </summary>
        public long OrderCode { get; set; }

        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = "payos";

        /// <summary>
        /// PENDING | SUCCESS | CANCELLED | FAILED
        /// </summary>
        public string PaymentStatus { get; set; } = "PENDING";

        /// <summary>
        /// PayOS transaction reference
        /// </summary>
        public string? TransactionId { get; set; }

        public string? PaymentUrl { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }

        public User User { get; set; } = null!;
        public SubscriptionPlan Plan { get; set; } = null!;
    }
}