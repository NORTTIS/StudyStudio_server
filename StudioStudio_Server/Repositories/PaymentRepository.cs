using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    public class PaymentRepository : IPaymentRepository
    {
        private readonly StudioDbContext _db;

        public PaymentRepository(StudioDbContext db)
        {
            _db = db;
        }

        public async Task<Payment?> GetByOrderCodeAsync(long orderCode)
        {
            return await _db.Payments
                .Include(p => p.Plan)
                .FirstOrDefaultAsync(p => p.OrderCode == orderCode);
        }

        public async Task<Payment?> GetByPaymentIdAsync(Guid paymentId)
        {
            return await _db.Payments
                .Include(p => p.Plan)
                .FirstOrDefaultAsync(p => p.PaymentId == paymentId);
        }

        public async Task AddAsync(Payment payment)
        {
            await _db.Payments.AddAsync(payment);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(Payment payment)
        {
            _db.Payments.Update(payment);
            await _db.SaveChangesAsync();
        }

        public async Task<List<Payment>> GetByUserIdAsync(Guid userId)
        {
            return await _db.Payments
                .Where(p => p.UserId == userId)
                .ToListAsync();
        }
    }
}
