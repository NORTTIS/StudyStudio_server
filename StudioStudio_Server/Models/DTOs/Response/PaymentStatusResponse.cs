namespace StudioStudio_Server.Models.DTOs.Response
{
    public class PaymentStatusResponse
    {
        public Guid PaymentId { get; set; }
        public long OrderCode { get; set; }
        public Enums.PaymentStatus PaymentStatus { get; set; }
        public decimal Amount { get; set; }
        public string PlanName { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
    }
}
