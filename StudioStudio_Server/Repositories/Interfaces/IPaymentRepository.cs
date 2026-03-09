using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface IPaymentRepository
    {
        Task<Payment?> GetByOrderCodeAsync(long orderCode);
        Task<Payment?> GetByPaymentIdAsync(Guid paymentId);
        Task AddAsync(Payment payment);
        Task UpdateAsync(Payment payment);
        Task<List<Payment>> GetByUserIdAsync(Guid userId);
    }
}
