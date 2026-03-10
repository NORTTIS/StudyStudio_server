using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using PaymentStatusEnum = StudioStudio_Server.Models.Enums.PaymentStatus;

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

        public async Task<(List<Payment> Items, int TotalCount)> GetBillingHistoryAsync(
            string? searchTerm,
            PaymentStatusEnum? paymentStatus,
            int pageNumber,
            int pageSize)
        {
            var query = _db.Payments
                .Include(p => p.User)
                .Include(p => p.Plan)
                .AsQueryable();

            // Filter by payment status
            if (paymentStatus.HasValue)
            {
                query = query.Where(p => p.PaymentStatus == paymentStatus.Value);
            }

            // Search by userName, userEmail, or orderCode (invoiceId)
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var lowerSearchTerm = searchTerm.ToLower();
                query = query.Where(p =>
                    (p.User.FirstName + " " + p.User.LastName).ToLower().Contains(lowerSearchTerm) ||
                    p.User.Email.ToLower().Contains(lowerSearchTerm) ||
                    p.OrderCode.ToString().Contains(lowerSearchTerm));
            }

            // Get total count before pagination
            var totalCount = await query.CountAsync();

            // Apply pagination and ordering
            var items = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<List<Payment>> GetAllPendingByUserIdAsync(Guid userId)
        {
            return await _db.Payments
                .Where(p => p.UserId == userId && p.PaymentStatus == "PENDING")
                .ToListAsync();
        }
    }
}
