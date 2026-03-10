using StudioStudio_Server.Models.Entities;
using PaymentStatusEnum = StudioStudio_Server.Models.Enums.PaymentStatus;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IPaymentRepository
    {
        Task<Payment?> GetByOrderCodeAsync(long orderCode);
        Task<Payment?> GetByPaymentIdAsync(Guid paymentId);
        Task AddAsync(Payment payment);
        Task UpdateAsync(Payment payment);
        Task<List<Payment>> GetByUserIdAsync(Guid userId);
        Task<List<Payment>> GetAllPendingByUserIdAsync(Guid userId);

        /// <summary>
        /// Get paginated billing history with filters (admin)
        /// Search by: userName, userEmail, orderCode (invoiceId)
        /// Filter by: paymentStatus (enum)
        /// </summary>
        Task<(List<Payment> Items, int TotalCount)> GetBillingHistoryAsync(
            string? searchTerm,
            PaymentStatusEnum? paymentStatus,
            int pageNumber,
            int pageSize);
    }
}
