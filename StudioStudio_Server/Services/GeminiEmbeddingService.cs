using StudioStudio_Server.Services.Interfaces;
using StudioStudio_Server.Configurations;
using StudioStudio_Server.Exceptions;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service tạo embeddings sử dụng Google Gemini API
    /// Model: gemini-embedding-001 (768 dimensions)
    /// </summary>
    public class GeminiEmbeddingService : IEmbeddingService
    {
        private readonly GeminiConfig _config;
        private readonly ILogger<GeminiEmbeddingService> _logger;
        private readonly HttpClient _httpClient;
        private const string GEMINI_ENDPOINT = "https://generativelanguage.googleapis.com/v1beta/models";
        private readonly SemaphoreSlim _semaphore = new(5);
        public string ModelName => _config.Model;

        /// <summary>
        /// Khởi tạo Gemini Embedding Service
        /// Note: Nếu API key không có, service hoạt động ở degraded mode
        /// </summary>
        public GeminiEmbeddingService(
            IOptions<GeminiConfig> config,
            ILogger<GeminiEmbeddingService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _config = config.Value;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);

            if (string.IsNullOrEmpty(_config.ApiKey))
            {
                _logger.LogWarning("Gemini API Key chưa được cấu hình. Embedding operations sẽ bị bỏ qua.");
            }
            else
            {
                _httpClient.DefaultRequestHeaders.Remove("x-goog-api-key");
                _httpClient.DefaultRequestHeaders.Add("x-goog-api-key", _config.ApiKey);
            }
        }

        /// <summary>
        /// Tạo embedding vector từ text
        /// Model: gemini-embedding-001 (768 dims)
        /// </summary>
        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            return await GenerateEmbeddingAsync(text, CancellationToken.None);
        }

        /// <summary>
        /// Tạo embedding vector từ text với CancellationToken
        /// </summary>
        public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(_config.ApiKey))
            {
                _logger.LogError("Gemini API Key chưa được cấu hình.");
                throw new AppException(
                    ErrorCodes.ConfigurationMissing,
                    StatusCodes.Status500InternalServerError);
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Text không được rỗng", nameof(text));
            }

            if (text.Length > _config.MaxTextLength)
            {
                _logger.LogWarning("Text length ({Length}) vượt quá giới hạn ({Max}). Truncating...",
                    text.Length, _config.MaxTextLength);
                text = text.Substring(0, _config.MaxTextLength);
            }

            Stopwatch sw = Stopwatch.StartNew();

            for (int attempt = 1; attempt <= _config.RetryAttempts; attempt++)
            {
                try
                {
                    float[] embedding = await GenerateEmbeddingInternalAsync(text, cancellationToken);

                    sw.Stop();
                    _logger.LogDebug("Gemini embedding tạo thành công. Vector size: {Size}, Latency: {Ms}ms",
                        embedding.Length, sw.ElapsedMilliseconds);

                    return embedding;
                }
                catch (HttpRequestException ex) when (attempt < _config.RetryAttempts && IsTransientError(ex))
                {
                    _logger.LogWarning("Gemini API transient error on attempt {Attempt}/{Max}. Retrying in {Delay}ms...",
                        attempt, _config.RetryAttempts, _config.RetryDelayMs);
                    await Task.Delay(_config.RetryDelayMs, cancellationToken);
                }
                catch (Exception ex) when (attempt < _config.RetryAttempts)
                {
                    _logger.LogWarning(ex, "Gemini API error on attempt {Attempt}/{Max}. Retrying...",
                        attempt, _config.RetryAttempts);
                    await Task.Delay(_config.RetryDelayMs, cancellationToken);
                }
            }

            throw new AppException(
                ErrorCodes.ExternalServiceError,
                StatusCodes.Status503ServiceUnavailable);
        }

        /// <summary>
        /// Internal method để generate embedding (không retry)
        /// </summary>
        private async Task<float[]> GenerateEmbeddingInternalAsync(
    string text,
    CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                string url = $"{GEMINI_ENDPOINT}/{_config.Model}:embedContent";

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

                HttpResponseMessage response =
                    await _httpClient.PostAsync(url, content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent =
                        await response.Content.ReadAsStringAsync(cancellationToken);

                    if ((int)response.StatusCode == 429 ||
                        (int)response.StatusCode >= 500)
                    {
                        throw new HttpRequestException(
                            $"Transient error: {response.StatusCode}");
                    }

                    _logger.LogError("Gemini API error: {Error}", errorContent);

                    throw new AppException(
                        ErrorCodes.ExternalServiceError,
                        StatusCodes.Status500InternalServerError);
                }

                string jsonResponse =
                    await response.Content.ReadAsStringAsync(cancellationToken);

                using var doc = JsonDocument.Parse(jsonResponse);

                float[] embedding = doc.RootElement
                    .GetProperty("embedding")
                    .GetProperty("values")
                    .EnumerateArray()
                    .Select(e => e.GetSingle())
                    .ToArray();

                if (embedding.Length != _config.ExpectedDimension)
                {
                    _logger.LogError("Invalid embedding dimension. Expected: {Expected}, Got: {Actual}",
                        _config.ExpectedDimension, embedding.Length);

                    throw new AppException(
                        ErrorCodes.ExternalServiceError,
                        StatusCodes.Status500InternalServerError);
                }

                if (_config.ExpectedDimension != 3072)
                {
                    embedding = Normalize(embedding);
                }

                return embedding;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        /// <summary>
        /// Tạo embeddings cho nhiều texts (batch processing)
        /// Note: Gemini API có batch endpoint riêng
        /// </summary>
        public async Task<List<float[]>> GenerateBatchEmbeddingsAsync(List<string> texts)
        {
            return await GenerateBatchEmbeddingsAsync(texts, CancellationToken.None);
        }

        /// <summary>
        /// Tạo embeddings cho nhiều texts (batch processing) với CancellationToken
        /// Note: Gemini API có batch endpoint riêng
        /// </summary>
        public async Task<List<float[]>> GenerateBatchEmbeddingsAsync(
             List<string> texts,
             CancellationToken cancellationToken)
        {
            if (texts == null || texts.Count == 0)
                return new List<float[]>();

            if (texts.Count > _config.MaxBatchSize)
                texts = texts.Take(_config.MaxBatchSize).ToList();

            texts = texts.Select(t =>
                t.Length > _config.MaxTextLength
                    ? t.Substring(0, _config.MaxTextLength)
                    : t
            ).ToList();

            var tasks = texts.Select(text =>
                GenerateEmbeddingAsync(text, cancellationToken));

            return (await Task.WhenAll(tasks)).ToList();
        }

        /// <summary>
        /// Kiểm tra xem lỗi có phải transient error không (có thể retry)
        /// </summary>
        private bool IsTransientError(HttpRequestException ex)
        {
            string message = ex.Message.ToLower();
            return message.Contains("429") ||
                   message.Contains("500") ||
                   message.Contains("503") ||
                   message.Contains("timeout") ||
                   message.Contains("network");
        }
        private float[] Normalize(float[] vector)
        {
            double norm = Math.Sqrt(vector.Sum(x => x * x));
            if (norm == 0) return vector;

            return vector.Select(x => (float)(x / norm)).ToArray();
        }
    }
}
