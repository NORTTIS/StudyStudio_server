namespace StudioStudio_Server.Models.DTOs.Response
{
    public class CreatePaymentResponse
    {
        public Guid PaymentId { get; set; }
        public long OrderCode { get; set; }
        public string PaymentUrl { get; set; } = null!;
        public decimal Amount { get; set; }
        public string PlanName { get; set; } = null!;
        public DateTimeOffset ExpiredAt { get; set; }
    }
}
