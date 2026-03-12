using Microsoft.Extensions.Options;
using StudioStudio_Server.Configurations;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Services.Interfaces;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service responsible for generating text embeddings using Google Gemini API.
    /// Implements rate-limiting, retry logic, and batch processing for efficient embedding generation.
    /// Model: gemini-embedding-001 (768-dimensional vectors)
    /// </summary>
    /// <remarks>
    /// This service handles:
    /// - Single text embedding generation for search queries
    /// - Batch embedding generation for document processing
    /// - Automatic retry with exponential backoff for transient errors
    /// - Sequential processing to respect API rate limits (15 RPM for free tier)
    /// - Vector normalization for consistent similarity calculations
    /// </remarks>
    public class GeminiEmbeddingService : IEmbeddingService
    {
        private readonly GeminiConfig _config;
        private readonly ILogger<GeminiEmbeddingService> _logger;
        private readonly HttpClient _httpClient;
        private const string GEMINI_ENDPOINT = "https://generativelanguage.googleapis.com/v1beta/models";

        /// <summary>
        /// Semaphore to ensure only one concurrent request to Gemini API.
        /// This prevents rate limiting errors by serializing all API calls.
        /// </summary>
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        /// <summary>
        /// Gets the name of the embedding model being used.
        /// </summary>
        public string ModelName => _config.Model;

        /// <summary>
        /// Initializes the Gemini Embedding Service with configuration and dependencies.
        /// Sets up HTTP client with API key and timeout settings.
        /// </summary>
        /// <param name="config">Configuration containing API key and rate limit settings</param>
        /// <param name="logger">Logger for tracking embedding operations</param>
        /// <param name="httpClientFactory">Factory for creating HTTP clients</param>
        /// <remarks>
        /// If API key is not configured, the service will log a warning and throw exceptions
        /// when embedding generation is attempted. This allows the application to start
        /// without crashing, but API calls will fail with ConfigurationMissing errors.
        /// </remarks>
        public GeminiEmbeddingService(
            IOptions<GeminiConfig> config,
            ILogger<GeminiEmbeddingService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _config = config.Value;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);

            // Configure HTTP client with Gemini API key if available
            if (string.IsNullOrEmpty(_config.ApiKey))
            {
                _logger.LogWarning("Gemini API Key not configured. Embedding operations will fail at runtime.");
            }
            else
            {
                // Remove any existing key header before adding new one to prevent duplicates
                _httpClient.DefaultRequestHeaders.Remove("x-goog-api-key");
                _httpClient.DefaultRequestHeaders.Add("x-goog-api-key", _config.ApiKey);
            }
        }


        /// <summary>
        /// Generates an embedding vector for a single text (convenience overload without cancellation).
        /// Primarily used for search queries and single-text operations.
        /// </summary>
        /// <param name="text">Text content to generate embedding for (max 10,000 characters)</param>
        /// <returns>768-dimensional float array representing the text embedding</returns>
        /// <exception cref="AppException">Thrown when API key is missing or API call fails</exception>
        /// <exception cref="ArgumentException">Thrown when text is null or empty</exception>
        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            return await GenerateEmbeddingAsync(text, CancellationToken.None);
        }

        /// <summary>
        /// Generates an embedding vector for a single text with cancellation support.
        /// Includes automatic retry with exponential backoff for transient failures.
        /// </summary>
        /// <param name="text">Text content to generate embedding for</param>
        /// <param name="cancellationToken">Token to cancel the operation</param>
        /// <returns>768-dimensional float array representing the text embedding</returns>
        /// <exception cref="AppException">Thrown when API key is missing, validation fails, or all retry attempts exhausted</exception>
        /// <exception cref="ArgumentException">Thrown when text is null or empty</exception>
        /// <remarks>
        /// Retry logic:
        /// - 429 (Rate Limit): Exponential backoff with 2x multiplier per attempt
        /// - 500/503 (Server Error): Standard retry delay
        /// - Network errors: Standard retry delay
        /// Maximum retry attempts controlled by configuration (default: 3)
        /// </remarks>
        public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken)
        {
            // Validate API key configuration
            if (string.IsNullOrEmpty(_config.ApiKey))
            {
                _logger.LogError("Gemini API Key not configured. Cannot generate embeddings.");
                throw new AppException(
                    ErrorCodes.ConfigurationMissing,
                    StatusCodes.Status500InternalServerError);
            }

            // Validate input text
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Text cannot be null or empty", nameof(text));
            }

            // Truncate text if it exceeds API limit
            if (text.Length > _config.MaxTextLength)
            {
                _logger.LogWarning(
                    "Text length ({Length} chars) exceeds limit ({Max} chars). Truncating to prevent API error.",
                    text.Length, _config.MaxTextLength);

                text = text.Substring(0, _config.MaxTextLength);
            }

            Stopwatch sw = Stopwatch.StartNew();

            // Retry loop with exponential backoff
            for (int attempt = 1; attempt <= _config.RetryAttempts; attempt++)
            {
                try
                {
                    float[] embedding = await GenerateEmbeddingInternalAsync(text, cancellationToken);

                    sw.Stop();
                    _logger.LogDebug(
                        "Embedding generated successfully. Dimensions: {Size}, Latency: {Ms}ms, Attempt: {Attempt}",
                        embedding.Length, sw.ElapsedMilliseconds, attempt);

                    return embedding;
                }
                catch (HttpRequestException ex) when (attempt < _config.RetryAttempts && IsTransientError(ex))
                {
                    // Calculate delay with exponential backoff for rate limiting errors
                    // 429 errors get 2x longer delays: attempt 1 = 4s, attempt 2 = 8s, attempt 3 = 16s
                    int delayMs = ex.Message.Contains("429")
                        ? _config.RetryDelayMs * attempt * 2
                        : _config.RetryDelayMs * attempt;

                    _logger.LogWarning(
                        "Transient API error on attempt {Attempt}/{Max}. Retrying in {Delay}ms. Error: {Error}",
                        attempt, _config.RetryAttempts, delayMs, ex.Message);

                    await Task.Delay(delayMs, cancellationToken);
                }
                catch (Exception ex) when (attempt < _config.RetryAttempts)
                {
                    // Generic retry for non-HTTP errors
                    _logger.LogWarning(ex,
                        "Unexpected error on attempt {Attempt}/{Max}. Retrying after {Delay}ms...",
                        attempt, _config.RetryAttempts, _config.RetryDelayMs);

                    await Task.Delay(_config.RetryDelayMs, cancellationToken);
                }
            }

            // All retry attempts exhausted
            _logger.LogError("Failed to generate embedding after {Attempts} attempts", _config.RetryAttempts);
            throw new AppException(
                ErrorCodes.ExternalServiceError,
                StatusCodes.Status503ServiceUnavailable);
        }

        /// <summary>
        /// Generates embeddings for multiple texts (convenience overload without cancellation).
        /// Used for batch document processing operations.
        /// </summary>
        /// <param name="texts">List of text chunks to generate embeddings for</param>
        /// <returns>List of 768-dimensional embedding vectors, one per input text</returns>
        public async Task<List<float[]>> GenerateBatchEmbeddingsAsync(List<string> texts)
        {
            return await GenerateBatchEmbeddingsAsync(texts, CancellationToken.None);
        }

        /// <summary>
        /// Generates embeddings for multiple texts with sequential processing to respect rate limits.
        /// Automatically splits large batches and adds configurable delays between requests.
        /// </summary>
        /// <param name="texts">List of text chunks to generate embeddings for</param>
        /// <param name="cancellationToken">Token to cancel the operation</param>
        /// <returns>List of 768-dimensional embedding vectors, one per input text</returns>
        /// <exception cref="InvalidOperationException">Thrown if embedding count doesn't match input count</exception>
        /// <remarks>
        /// Processing strategy:
        /// 1. Truncate any text exceeding max length (10,000 chars)
        /// 2. Split into batches if count > MaxBatchSize (default: 10)
        /// 3. Process each text sequentially with configurable delay
        /// 4. Add longer delay between batches to prevent rate limiting
        /// </remarks>
        public async Task<List<float[]>> GenerateBatchEmbeddingsAsync(
            List<string> texts,
            CancellationToken cancellationToken)
        {
            // Validate input
            if (texts == null || texts.Count == 0)
            {
                _logger.LogWarning("No texts provided for batch embedding generation. Returning empty list.");
                return new List<float[]>();
            }

            // Truncate any texts exceeding max length to prevent API errors
            texts = texts.Select(t =>
                t.Length > _config.MaxTextLength
                    ? t.Substring(0, _config.MaxTextLength)
                    : t
            ).ToList();

            _logger.LogInformation(
                "Starting batch embedding generation for {Count} texts (sequential processing to respect rate limits)",
                texts.Count);

            try
            {
                List<float[]> allEmbeddings = new List<float[]>(texts.Count);

                // Process all texts sequentially with rate limiting
                for (int i = 0; i < texts.Count; i++)
                {
                    // Generate embedding for current text
                    float[] embedding = await GenerateEmbeddingAsync(texts[i], cancellationToken);
                    allEmbeddings.Add(embedding);

                    // Log progress for large batches
                    if (texts.Count > 10 || (i + 1) % 10 == 0)
                    {
                        _logger.LogInformation(
                            "Batch progress: {Current}/{Total} embeddings generated ({Percent}%)",
                            i + 1, texts.Count, (int)((i + 1) * 100.0 / texts.Count));
                    }

                    // Add delay between requests to respect API rate limits
                    // Skip delay after last item
                    if (i < texts.Count - 1)
                    {
                        await Task.Delay(_config.DelayBetweenRequestsMs, cancellationToken);
                    }
                }

                // Validate that all embeddings were generated successfully
                if (allEmbeddings.Count != texts.Count)
                {
                    _logger.LogError(
                        "Batch embedding count mismatch. Expected: {Expected}, Generated: {Actual}",
                        texts.Count, allEmbeddings.Count);

                    throw new InvalidOperationException(
                        $"Failed to generate all embeddings. Expected: {texts.Count}, Got: {allEmbeddings.Count}");
                }

                _logger.LogInformation(
                    "Batch embedding generation completed successfully. Generated {Count} embeddings",
                    allEmbeddings.Count);

                return allEmbeddings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Batch embedding generation failed after partial completion");
                throw;
            }
        }


        /// <summary>
        /// Internal method that makes the actual HTTP request to Gemini API.
        /// This method has no retry logic - retries are handled by the public wrapper.
        /// </summary>
        /// <param name="text">Text to generate embedding for</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>768-dimensional embedding vector, normalized to unit length</returns>
        /// <exception cref="HttpRequestException">Thrown for transient HTTP errors (429, 500, 503)</exception>
        /// <exception cref="AppException">Thrown for permanent errors or validation failures</exception>
        /// <remarks>
        /// This method is protected by a semaphore to ensure only one concurrent API call.
        /// The semaphore prevents race conditions and helps respect rate limits.
        /// </remarks>
        private async Task<float[]> GenerateEmbeddingInternalAsync(
            string text,
            CancellationToken cancellationToken)
        {
            // Wait for semaphore to ensure sequential API calls (rate limit protection)
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                string url = $"{GEMINI_ENDPOINT}/{_config.Model}:embedContent";

                // Build request body according to Gemini API specification
                object requestBody = new
                {
                    model = $"models/{_config.Model}",
                    content = new
                    {
                        parts = new[]
                        {
                            new { text = text }
                        }
                    },
                    outputDimensionality = _config.OutputDimensionality
                };

                StringContent content = new(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");

                // Make HTTP POST request to Gemini API
                HttpResponseMessage response = await _httpClient.PostAsync(url, content, cancellationToken);

                // Handle error responses
                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

                    // Throw HttpRequestException for transient errors (will be caught and retried)
                    if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
                    {
                        throw new HttpRequestException($"Transient error: {response.StatusCode}");
                    }

                    // Permanent error - throw AppException (will not be retried)
                    _logger.LogError("Gemini API returned permanent error. Status: {Status}, Body: {Error}",
                        response.StatusCode, errorContent);

                    throw new AppException(
                        ErrorCodes.ExternalServiceError,
                        StatusCodes.Status500InternalServerError);
                }

                // Parse successful response
                string jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
                using JsonDocument doc = JsonDocument.Parse(jsonResponse);

                // Extract embedding vector from JSON response
                float[] embedding = doc.RootElement
                    .GetProperty("embedding")
                    .GetProperty("values")
                    .EnumerateArray()
                    .Select(e => e.GetSingle())
                    .ToArray();

                // Validate embedding dimensions
                if (embedding.Length != _config.ExpectedDimension)
                {
                    _logger.LogError(
                        "Embedding dimension mismatch. Expected: {Expected}, Received: {Actual}",
                        _config.ExpectedDimension, embedding.Length);

                    throw new AppException(
                        ErrorCodes.ExternalServiceError,
                        StatusCodes.Status500InternalServerError);
                }

                // Normalize vector to unit length for consistent similarity calculations
                // Note: text-embedding-3-large (3072 dims) doesn't require normalization
                if (_config.ExpectedDimension != 3072)
                {
                    embedding = Normalize(embedding);
                }

                return embedding;
            }
            finally
            {
                // Always release semaphore to allow next request
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Determines if an HTTP error is transient and should be retried.
        /// </summary>
        /// <param name="ex">HTTP request exception to evaluate</param>
        /// <returns>True if error is transient (429, 500, 503, timeout, network), false otherwise</returns>
        /// <remarks>
        /// Transient errors are temporary issues that may resolve on retry:
        /// - 429: Rate limit exceeded (too many requests)
        /// - 500: Internal server error
        /// - 503: Service unavailable
        /// - Timeout: Request took too long
        /// - Network: Connection issues
        /// 
        /// Non-transient errors (400, 401, 403, 404) indicate client errors and should not be retried.
        /// </remarks>
        private bool IsTransientError(HttpRequestException ex)
        {
            string message = ex.Message.ToLower();
            return message.Contains("429") ||
                   message.Contains("500") ||
                   message.Contains("503") ||
                   message.Contains("timeout") ||
                   message.Contains("network");
        }

        /// <summary>
        /// Normalizes a vector to unit length (L2 normalization).
        /// This ensures consistent cosine similarity calculations in vector search.
        /// </summary>
        /// <param name="vector">Input vector to normalize</param>
        /// <returns>Normalized vector with magnitude = 1</returns>
        /// <remarks>
        /// Formula: normalized_vector = vector / ||vector||
        /// Where ||vector|| is the L2 norm (Euclidean length).
        /// 
        /// Special case: If norm is 0 (zero vector), returns the original vector unchanged
        /// to avoid division by zero.
        /// 
        /// Why normalize?
        /// - Enables consistent cosine similarity: dot(a, b) when both are normalized
        /// - Required by many vector databases (e.g., Qdrant with cosine metric)
        /// - Prevents magnitude from affecting similarity scores
        /// </remarks>
        private float[] Normalize(float[] vector)
        {
            // Calculate L2 norm (Euclidean length): sqrt(sum of squares)
            double norm = Math.Sqrt(vector.Sum(x => x * x));

            // Handle zero vector edge case
            if (norm == 0)
            {
                return vector;
            }

            // Divide each component by norm to get unit vector
            return vector.Select(x => (float)(x / norm)).ToArray();
        }

        /// <summary>
        /// Test connection to Gemini API
        /// </summary>
        public async Task TestConnectionAsync()
        {
            if (string.IsNullOrEmpty(_config.ApiKey))
            {
                throw new Exception("Gemini API key not configured");
            }

            // Test with a simple embedding request
            try
            {
                await GenerateEmbeddingAsync("test");
            }
            catch (Exception ex)
            {
                // If it's a configuration error, rethrow
                if (ex.Message.Contains("not configured"))
                {
                    throw;
                }
                // For other errors (like API quota), we consider it a connection issue
                throw new Exception($"Gemini API connection failed: {ex.Message}");
            }
        }

    }
}
