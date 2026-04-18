namespace StudioStudio_Server.Services.Interfaces
{
    /// <summary>
    /// Tracks token usage from LLM API responses for accurate billing and analytics.
    /// </summary>
    public record TokenUsage(
        int InputTokens,
        int OutputTokens,
        int CachedTokens = 0,
        int ThinkingTokens = 0);

    /// <summary>
    /// Service interface cho LLM (Large Language Model) inference
    /// Implementation: GeminiLLMService su dung Gemini 2.5 Flash (primary) voi fallback sang Gemini 1.5 Flash
    /// </summary>
    public interface ILLMService
    {
        /// <summary>
        /// Goi LLM API de generate cau tra loi + token usage
        /// </summary>
        /// <param name="systemPrompt">System prompt de dinh nghia behavior cua AI</param>
        /// <param name="userMessage">Cau hoi tu user</param>
        /// <param name="context">Context tu documents va tasks</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Tuple of (answer, tokenUsage) tu LLM</returns>
        Task<(string Answer, TokenUsage Usage)> GenerateAnswerWithUsageAsync(
            string systemPrompt,
            string userMessage,
            string context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Goi LLM API va tra ve raw text khong ap response schema co dinh.
        /// Dung cho cac tac vu phan tich noi bo nhu parameter review.
        /// </summary>
        /// <param name="systemPrompt">System prompt cho tac vu cap nhat</param>
        /// <param name="userMessage">Noi dung can xu ly</param>
        /// <param name="context">Context bo sung</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Raw text response tu LLM</returns>
        Task<string> GenerateTextResponseAsync(
            string systemPrompt,
            string userMessage,
            string context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Goi LLM API de generate cau tra loi dang streaming
        /// </summary>
        /// <param name="systemPrompt">System prompt de dinh nghia behavior cua AI</param>
        /// <param name="userMessage">Cau hoi tu user</param>
        /// <param name="context">Context tu documents va tasks</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stream of text chunks from LLM</returns>
        IAsyncEnumerable<string> GenerateAnswerStreamAsync(
            string systemPrompt,
            string userMessage,
            string context,
            CancellationToken cancellationToken = default,
            bool forceTextMode = false);
    }
}
