namespace StudioStudio_Server.Configurations
{
    /// <summary>
    /// C?u h?nh cho Google Gemini API
    /// Model: gemini-embedding-001 (768 dimensions)
    /// </summary>
    public class GeminiConfig
    {
        /// <summary>
        /// Gemini API Key (from Google AI Studio)
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Embedding model name
        /// Default: gemini-embedding-001
        /// </summary>
        public string Model { get; set; } = "gemini-embedding-001";

        /// <summary>
        /// Expected embedding dimension for validation
        /// Default: 768 for gemini-embedding-001
        /// </summary>
        public int ExpectedDimension { get; set; } = 768;

        /// <summary>
        /// Output embedding dimension
        /// Default: 768 for gemini-embedding-001
        /// </summary>
        public int OutputDimensionality { get; set; } = 768;

        /// <summary>
        /// API timeout in seconds
        /// Default: 30 seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Maximum text length in characters
        /// Default: 10000 characters
        /// </summary>
        public int MaxTextLength { get; set; } = 10000;

        /// <summary>
        /// Maximum batch size for batch embedding
        /// Default: 20
        /// </summary>
        public int MaxBatchSize { get; set; } = 20;

        /// <summary>
        /// Number of retry attempts for transient failures
        /// Default: 3
        /// </summary>
        public int RetryAttempts { get; set; } = 3;

        /// <summary>
        /// Retry delay in milliseconds
        /// Default: 1000ms
        /// </summary>
        public int RetryDelayMs { get; set; } = 1000;
    }
}
