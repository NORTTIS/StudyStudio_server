namespace StudioStudio_Server.Models.Entities
{
    public class Payment
    {
        public Guid PaymentId { get; set; }

        public Guid UserId { get; set; }
        public Guid SubscriptionId { get; set; }

        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = null!;
        public string PaymentStatus { get; set; } = null!;

        public DateTime CreatedAt { get; set; }
    }
}