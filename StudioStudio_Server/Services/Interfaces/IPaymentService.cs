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
        Task<PaymentStatusResponse> CancelPaymentAsync(Guid userId, Guid paymentId);
        Task<PaymentHistoryResponse> GetPaymentHistoryAsync(Guid userId);
    }
}
