using PayOS.Models.Webhooks;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IPaymentService
    {
        Task<CreatePaymentResponse> CreatePaymentLinkAsync(Guid userId, CreatePaymentRequest request);
        Task HandleWebhookAsync(Webhook webhook);
        Task<PaymentStatusResponse> GetPaymentStatusAsync(Guid userId, Guid paymentId);
        Task<PaymentStatusResponse> CancelPaymentAsync(Guid userId, long orderCode);
        Task<PaymentHistoryResponse> GetPaymentHistoryAsync(Guid userId);

        /// <summary>
        /// [ADMIN] Get paginated billing history with filters
        /// Search by: userName, userEmail, invoiceId (orderCode)
        /// Filter by: paymentStatus
        /// </summary>
        Task<BillingHistoryResponse> GetBillingHistoryAsync(GetBillingHistoryRequest request);
    }
}
