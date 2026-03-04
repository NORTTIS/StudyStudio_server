using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository implementation cho AI Request Log tracking
    /// </summary>
    public class AIRequestLogRepository : IAIRequestLogRepository
    {
        private readonly StudioDbContext _context;

        public AIRequestLogRepository(StudioDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Log m?t AI request vào database
        /// </summary>
        public async Task AddAsync(AIRequestLog log)
        {
            _context.AIRequestLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Ð?m s? lý?ng AI requests c?a user trong ngày hi?n t?i
        /// Ði?u ki?n: UserId = {userId} AND CreatedAt >= {startOfDay}
        /// Use case: Rate limiting check (Free: 20/day, Premium: 100/day)
        /// </summary>
        public async Task<int> CountTodayRequestsAsync(Guid userId, DateTime startOfDay)
        {
            return await _context.AIRequestLogs
                .Where(log => log.UserId == userId && log.CreatedAt >= startOfDay)
                .CountAsync();
        }

        /// <summary>
        /// L?y t?ng s? tokens ð? s? d?ng trong ngày
        /// Use case: Usage analytics, billing
        /// </summary>
        public async Task<int> GetTodayTokenUsageAsync(Guid userId, DateTime startOfDay)
        {
            return await _context.AIRequestLogs
                .Where(log => log.UserId == userId && log.CreatedAt >= startOfDay)
                .SumAsync(log => log.TokenUsed);
        }
    }
}
