using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    /// <summary>
    /// Repository interface cho AI Request Log tracking
    /// Used for: Rate limiting, usage analytics, billing
    /// </summary>
    public interface IAIRequestLogRepository
    {
        /// <summary>
        /// Log m?t AI request
        /// </summary>
        /// <param name="log">AI request log entity</param>
        Task AddAsync(AIRequestLog log);

        /// <summary>
        /// Đếm số lượng AI requests của user trong 1 ngày
        /// Điều kiện: UserId = {userId} AND CreatedAt >= startOfDay
        /// Use case: Check rate limiting (Free: 20/day, Premium: 100/day)
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="startOfDay">Start of current day (UTC)</param>
        /// <returns>Number of requests today</returns>
        Task<int> CountTodayRequestsAsync(Guid userId, DateTime startOfDay);

        /// <summary>
        /// Lấy tổng tokens đã sử dụng trong ngày (Input + Output + Cached + Thinking)
        /// Use case: Usage analytics, billing
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="startOfDay">Start of current day (UTC)</param>
        /// <returns>Total tokens used today</returns>
        Task<int> GetTodayTokenUsageAsync(Guid userId, DateTime startOfDay);

        /// <summary>
        /// Lấy chi tiết token usage trong ngày
        /// Use case: Detailed analytics dashboard
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="startOfDay">Start of current day (UTC)</param>
        /// <returns>Tuple of (InputTokens, OutputTokens, CachedTokens, ThinkingTokens, ToolCalls)</returns>
        Task<(int InputTokens, int OutputTokens, int CachedTokens, int ThinkingTokens, int ToolCalls)>
            GetTodayTokenUsageDetailAsync(Guid userId, DateTime startOfDay);
    }
}
