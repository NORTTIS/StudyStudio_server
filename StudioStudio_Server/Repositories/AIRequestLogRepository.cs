using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository implementation cho AI Request Log tracking
    /// </summary>
    public class AIRequestLogRepository(StudioDbContext context) : IAIRequestLogRepository
    {
        /// <summary>
        /// Log m?t AI request vào database
        /// </summary>
        public async Task AddAsync(AIRequestLog log)
        {
            context.AIRequestLogs.Add(log);
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Đếm số lượng AI requests của user trong ngày hiện tại
        /// Điều kiện: UserId = {userId} AND CreatedAt >= {startOfDay}
        /// Use case: Rate limiting check (Free: 20/day, Premium: 100/day)
        /// </summary>
        public async Task<int> CountTodayRequestsAsync(Guid userId, DateTime startOfDay)
        {
            return await context.AIRequestLogs
                .Where(log => log.UserId == userId && log.CreatedAt >= startOfDay)
                .CountAsync();
        }

        /// <summary>
        /// Lấy tổng số tokens đã sử dụng trong ngày (all types: Input + Output + Cached + Thinking)
        /// Use case: Usage analytics, billing
        /// </summary>
        public async Task<int> GetTodayTokenUsageAsync(Guid userId, DateTime startOfDay)
        {
            return await context.AIRequestLogs
                .Where(log => log.UserId == userId && log.CreatedAt >= startOfDay)
                .SumAsync(log =>
                    (log.InputTokens > 0 ? log.InputTokens : log.TokenUsed) +
                    (log.OutputTokens > 0 ? log.OutputTokens : 0) +
                    (log.CachedTokens > 0 ? log.CachedTokens : 0) +
                    (log.ThinkingTokens > 0 ? log.ThinkingTokens : 0));
        }
    }
}
