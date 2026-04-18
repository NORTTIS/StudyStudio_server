namespace StudioStudio_Server.Services.Interfaces
{
    /// <summary>
    /// Interface for text embedding operations
    /// Implementations: GeminiEmbeddingService, OpenAIEmbeddingService, HuggingFaceEmbeddingService
    /// </summary>
    public interface IEmbeddingService
    {
        string ModelName { get; }

        /// <summary>
        /// Generate vector embedding from text
        /// </summary>
        /// <param name="text">Text content to embed</param>
        /// <returns>Vector embedding (float array)</returns>
        Task<float[]> GenerateEmbeddingAsync(string text);

        /// <summary>
        /// Generate vector embedding from text with cancellation support
        /// </summary>
        /// <param name="text">Text content to embed</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Vector embedding (float array)</returns>
        Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken);

        /// <summary>
        /// Test connection to embedding service (for health checks)
        /// </summary>
        Task TestConnectionAsync();
    }
}
