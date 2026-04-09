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
        /// Đếm số lượng AI requests của user trong ngày hiện tại
        /// Điều kiện: UserId = {userId} AND CreatedAt >= {startOfDay}
        /// Use case: Rate limiting check (Free: 20/day, Premium: 100/day)
        /// </summary>
        public async Task<int> CountTodayRequestsAsync(Guid userId, DateTime startOfDay)
        {
            return await _context.AIRequestLogs
                .Where(log => log.UserId == userId && log.CreatedAt >= startOfDay)
                .CountAsync();
        }

        /// <summary>
        /// Lấy tổng số tokens đã sử dụng trong ngày (all types: Input + Output + Cached + Thinking)
        /// Use case: Usage analytics, billing
        /// </summary>
        public async Task<int> GetTodayTokenUsageAsync(Guid userId, DateTime startOfDay)
        {
            return await _context.AIRequestLogs
                .Where(log => log.UserId == userId && log.CreatedAt >= startOfDay)
                .SumAsync(log =>
                    (log.InputTokens > 0 ? log.InputTokens : log.TokenUsed) +
                    (log.OutputTokens > 0 ? log.OutputTokens : 0) +
                    (log.CachedTokens > 0 ? log.CachedTokens : 0) +
                    (log.ThinkingTokens > 0 ? log.ThinkingTokens : 0));
        }

        /// <summary>
        /// Lấy chi tiết token usage trong ngày
        /// Use case: Detailed analytics dashboard
        /// </summary>
        public async Task<(int InputTokens, int OutputTokens, int CachedTokens, int ThinkingTokens, int ToolCalls)>
            GetTodayTokenUsageDetailAsync(Guid userId, DateTime startOfDay)
        {
            var stats = await _context.AIRequestLogs
                .Where(log => log.UserId == userId && log.CreatedAt >= startOfDay)
                .GroupBy(log => 1)
                .Select(g => new
                {
                    InputTokens = g.Sum(l => l.InputTokens > 0 ? l.InputTokens : l.TokenUsed),
                    OutputTokens = g.Sum(l => l.OutputTokens > 0 ? l.OutputTokens : 0),
                    CachedTokens = g.Sum(l => l.CachedTokens > 0 ? l.CachedTokens : 0),
                    ThinkingTokens = g.Sum(l => l.ThinkingTokens > 0 ? l.ThinkingTokens : 0),
                    ToolCalls = g.Sum(l => l.ToolCallCount)
                })
                .FirstOrDefaultAsync();

            return stats != null
                ? (stats.InputTokens, stats.OutputTokens, stats.CachedTokens, stats.ThinkingTokens, stats.ToolCalls)
                : (0, 0, 0, 0, 0);
        }
    }
}
