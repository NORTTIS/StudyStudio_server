namespace StudioStudio_Server.Models.DTOs.Request
{
    public class CreatePaymentRequest
    {
        /// <summary>
        /// The subscription plan ID the user wants to purchase
        /// </summary>
        public Guid PlanId { get; set; }
    }
}
