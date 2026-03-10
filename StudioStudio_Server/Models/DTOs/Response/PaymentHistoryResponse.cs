namespace StudioStudio_Server.Models.DTOs.Response
{
    public class PaymentHistoryResponse
    {
        public List<PaymentHistory> PaymentHistories { get; set; } = new List<PaymentHistory>();
    }

    public class PaymentHistory
    {
        public Guid PaymentId { get; set; }
        public Guid PlanId { get; set; }
        public Enums.PaymentStatus Status { get; set; }
        public DateTime? PaidAt { get; set; }
    }
}
