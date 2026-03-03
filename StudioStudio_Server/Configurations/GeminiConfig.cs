namespace StudioStudio_Server.Configurations
{
    /// <summary>
    /// Configuration for Google Gemini API
    /// 
    /// Embedding Model: gemini-embedding-001 (768 dimensions)
    /// LLM Models: gemini-2.5-flash (primary), gemini-1.5-flash (fallback)
    /// 
    /// Rate Limits (Paid Tier):
    /// - RPM: 3,000 requests/minute
    /// - TPM: 1,000,000 tokens/minute (PRIMARY BOTTLENECK)
    /// - RPD: Unlimited
    /// 
    /// Processing Strategy:
    /// - Target: 800K tokens/minute (80% utilization, 20% safety margin)
    /// - Batch Size: 8 chunks per request (8 × 750 = 6,000 tokens/request)
    /// - Queue-based processing to prevent TPM limit violations
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
        /// Default: 30 seconds for embeddings, 60 seconds for LLM
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Maximum text length in characters per request
        /// Default: 10000 characters
        /// </summary>
        public int MaxTextLength { get; set; } = 10000;

        /// <summary>
        /// Maximum batch size for batch embedding
        /// Recommended: 8 chunks per batch (8 × 750 tokens = 6,000 tokens)
        /// This is safe and reduces API calls by 87.5%
        /// Default: 8
        /// </summary>
        public int MaxBatchSize { get; set; } = 8;

        /// <summary>
        /// Number of retry attempts for transient failures
        /// Default: 3
        /// </summary>
        public int RetryAttempts { get; set; } = 3;

        /// <summary>
        /// Retry delay in milliseconds
        /// Default: 500ms
        /// </summary>
        public int RetryDelayMs { get; set; } = 500;

        /// <summary>
        /// Delay between embedding requests in milliseconds
        /// Set to 0 for maximum throughput (rate limiting handled by token budget)
        /// Default: 0ms
        /// </summary>
        public int DelayBetweenRequestsMs { get; set; } = 0;

        /// <summary>
        /// Maximum tokens per minute (TPM limit)
        /// Target: 800,000 tokens/minute (80% of 1M limit, 20% safety margin)
        /// Default: 800000
        /// </summary>
        public int MaxTokensPerMinute { get; set; } = 800_000;

        /// <summary>
        /// Maximum requests per minute (RPM limit)
        /// Actual limit is 3,000 but set conservatively
        /// Default: 2900
        /// </summary>
        public int MaxRequestsPerMinute { get; set; } = 2_900;

        /// <summary>
        /// Enable token-based rate limiting
        /// When true, tracks token usage to respect TPM limit
        /// Default: true
        /// </summary>
        public bool EnableTokenRateLimiting { get; set; } = true;

        /// <summary>
        /// Maximum concurrent document processing tasks
        /// Set to 1 for strict sequential processing (safest)
        /// Can increase to 2-3 for better throughput if needed
        /// Default: 1
        /// </summary>
        public int MaxConcurrentDocuments { get; set; } = 1;

        // ============================================
        // LLM-specific Configuration (for Chat/Q&A)
        // ============================================

        /// <summary>
        /// LLM timeout in seconds (longer than embedding timeout)
        /// Default: 60 seconds
        /// </summary>
        public int LLMTimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// Maximum output tokens for LLM response
        /// Default: 2048 tokens (~8,000 characters)
        /// </summary>
        public int MaxTokens { get; set; } = 2048;

        /// <summary>
        /// Temperature for LLM response generation (0.0 - 2.0)
        /// Higher = more creative, Lower = more focused
        /// Default: 0.7
        /// </summary>
        public double Temperature { get; set; } = 0.7;

        /// <summary>
        /// Top P sampling for LLM (0.0 - 1.0)
        /// Controls diversity via nucleus sampling
        /// Default: 0.95
        /// </summary>
        public double TopP { get; set; } = 0.95;

        /// <summary>
        /// Top K sampling for LLM
        /// Limits vocabulary to top K tokens
        /// Default: 40
        /// </summary>
        public int TopK { get; set; } = 40;
    }
}
