namespace StudioStudio_Server.Services.Interfaces
{
    /// <summary>
    /// Service interface cho LLM (Large Language Model) inference
    /// Implementation: GeminiLLMService s? d?ng Gemini 2.5 Flash (primary) v?i fallback sang Gemini 1.5 Flash
    /// </summary>
    public interface ILLMService
    {
        /// <summary>
        /// G?i LLM API ð? generate câu tr? l?i
        /// </summary>
        /// <param name="systemPrompt">System prompt ð? ð?nh ngh?a behavior c?a AI</param>
        /// <param name="userMessage">Câu h?i t? user</param>
        /// <param name="context">Context t? documents và tasks</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Câu tr? l?i t? LLM</returns>
        Task<string> GenerateAnswerAsync(
            string systemPrompt,
            string userMessage,
            string context,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// G?i LLM API ð? generate câu tr? l?i d?ng streaming
        /// </summary>
        /// <param name="systemPrompt">System prompt ð? ð?nh ngh?a behavior c?a AI</param>
        /// <param name="userMessage">Câu h?i t? user</param>
        /// <param name="context">Context t? documents và tasks</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Stream c?a câu tr? l?i t? LLM</returns>
        IAsyncEnumerable<string> GenerateAnswerStreamAsync(
            string systemPrompt,
            string userMessage,
            string context,
            CancellationToken cancellationToken = default);
    }
}
