namespace StudioStudio_Server.Services.Interfaces
{
    /// <summary>
    /// Interface cho text embedding operations
    /// Implementations: GeminiEmbeddingService, OpenAIEmbeddingService, HuggingFaceEmbeddingService
    /// </summary>
    public interface IEmbeddingService
    {
        string ModelName { get; }

        /// <summary>
        /// T?o vector embedding t? text
        /// </summary>
        /// <param name="text">Text content ð? embedding</param>
        /// <returns>Vector embedding (float array)</returns>
        Task<float[]> GenerateEmbeddingAsync(string text);

        /// <summary>
        /// T?o vector embedding t? text v?i cancellation support
        /// </summary>
        /// <param name="text">Text content ð? embedding</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Vector embedding (float array)</returns>
        Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken);

        /// <summary>
        /// T?o embeddings cho nhi?u texts cùng lúc (batch)
        /// </summary>
        /// <param name="texts">Danh sách texts</param>
        /// <returns>Danh sách vectors týõng ?ng</returns>
        Task<List<float[]>> GenerateBatchEmbeddingsAsync(List<string> texts);

        /// <summary>
        /// T?o embeddings cho nhi?u texts cùng lúc v?i cancellation support
        /// </summary>
        /// <param name="texts">Danh sách texts</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Danh sách vectors týõng ?ng</returns>
        Task<List<float[]>> GenerateBatchEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken);
    }
}
