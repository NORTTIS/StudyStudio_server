namespace StudioStudio_Server.Services.Interfaces
{
    /// <summary>
    /// Service interface cho LLM (Large Language Model) inference
    /// S? d?ng Groq API ð? x? l? chat completion
    /// </summary>
    public interface ILLMService
    {
        /// <summary>
        /// G?i Groq API ð? generate câu tr? l?i
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
    }
}
