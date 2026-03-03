using Microsoft.Extensions.Options;
using StudioStudio_Server.Configurations;
using StudioStudio_Server.Services.Interfaces;
using System.Text;
using System.Text.Json;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service implementation for Gemini LLM API with automatic fallback
    /// Primary: gemini-2.5-flash (fast and efficient)
    /// Fallback: gemini-1.5-flash (when rate limited)
    /// </summary>
    public class GeminiLLMService : ILLMService
    {
        private readonly HttpClient _httpClient;
        private readonly GeminiConfig _config;
        private readonly ILogger<GeminiLLMService> _logger;
        private const string GEMINI_ENDPOINT = "https://generativelanguage.googleapis.com/v1beta/models";
        private const string PRIMARY_MODEL = "gemini-2.5-flash";
        private const string FALLBACK_MODEL = "gemini-1.5-flash";

        public GeminiLLMService(
            HttpClient httpClient,
            IOptions<GeminiConfig> config,
            ILogger<GeminiLLMService> logger)
        {
            _httpClient = httpClient;
            _config = config.Value;
            _logger = logger;

            // Configure HTTP client with LLM-specific timeout
            _httpClient.Timeout = TimeSpan.FromSeconds(_config.LLMTimeoutSeconds);

            if (string.IsNullOrEmpty(_config.ApiKey))
            {
                _logger.LogWarning("Gemini API Key not configured. LLM operations will fail at runtime.");
            }
        }

        /// <summary>
        /// Generates answer using Gemini LLM with automatic fallback on rate limit
        /// Flow:
        /// 1. Try PRIMARY_MODEL (gemini-2.5-flash)
        /// 2. If 429 (rate limit), fallback to FALLBACK_MODEL (gemini-1.5-flash)
        /// 3. If still fails, throw exception
        /// </summary>
        public async Task<string> GenerateAnswerAsync(
            string systemPrompt,
            string userMessage,
            string context,
            CancellationToken cancellationToken = default)
        {
            // Validate API key
            if (string.IsNullOrEmpty(_config.ApiKey))
            {
                _logger.LogError("Gemini API Key not configured. Cannot generate answer.");
                throw new InvalidOperationException("Gemini API Key is not configured");
            }

            // Try primary model first
            try
            {
                _logger.LogInformation("Attempting answer generation with PRIMARY model: {Model}", PRIMARY_MODEL);
                return await GenerateAnswerInternalAsync(
                    PRIMARY_MODEL,
                    systemPrompt,
                    userMessage,
                    context,
                    cancellationToken);
            }
            catch (HttpRequestException ex) when (IsRateLimitError(ex))
            {
                _logger.LogWarning(
                    "Rate limit hit on PRIMARY model ({Model}). Falling back to FALLBACK model ({Fallback})",
                    PRIMARY_MODEL, FALLBACK_MODEL);

                // Fallback to flash model
                try
                {
                    return await GenerateAnswerInternalAsync(
                        FALLBACK_MODEL,
                        systemPrompt,
                        userMessage,
                        context,
                        cancellationToken);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "FALLBACK model ({Model}) also failed", FALLBACK_MODEL);
                    throw new Exception($"Both PRIMARY and FALLBACK models failed. Last error: {fallbackEx.Message}", fallbackEx);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling PRIMARY model: {Model}", PRIMARY_MODEL);
                throw;
            }
        }

        /// <summary>
        /// Internal method to call Gemini API with specified model
        /// </summary>
        private async Task<string> GenerateAnswerInternalAsync(
            string modelName,
            string systemPrompt,
            string userMessage,
            string context,
            CancellationToken cancellationToken)
        {
            try
            {
                // Combine context with user message
                string fullMessage = $"{context}\n\nCâu h?i: {userMessage}";

                // Build request URL
                string url = $"{GEMINI_ENDPOINT}/{modelName}:generateContent?key={_config.ApiKey}";

                // Build request body according to Gemini API format
                var requestBody = new
                {
                    system_instruction = new
                    {
                        parts = new[]
                        {
                            new { text = systemPrompt }
                        }
                    },
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = new[]
                            {
                                new { text = fullMessage }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = _config.Temperature,
                        topK = _config.TopK,
                        topP = _config.TopP,
                        maxOutputTokens = _config.MaxTokens,
                        responseMimeType = "text/plain"
                    }
                };

                string jsonRequest = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                _logger.LogInformation("Calling Gemini API with model: {Model}", modelName);

                // Call API
                HttpResponseMessage response = await _httpClient.PostAsync(url, content, cancellationToken);

                // Handle error responses
                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    
                    _logger.LogError(
                        "Gemini API error. Status: {Status}, Model: {Model}, Error: {Error}",
                        response.StatusCode, modelName, errorContent);

                    // Throw HttpRequestException for rate limit (will be caught for fallback)
                    if ((int)response.StatusCode == 429)
                    {
                        throw new HttpRequestException($"Rate limit error (429) for model {modelName}");
                    }

                    throw new Exception($"Gemini API returned error: {response.StatusCode} - {errorContent}");
                }

                // Parse response
                string jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);

                using JsonDocument doc = JsonDocument.Parse(jsonResponse);
                JsonElement root = doc.RootElement;

                // Extract text from response
                // Response format: { "candidates": [{ "content": { "parts": [{ "text": "..." }] } }] }
                if (root.TryGetProperty("candidates", out JsonElement candidates) &&
                    candidates.GetArrayLength() > 0)
                {
                    JsonElement firstCandidate = candidates[0];
                    if (firstCandidate.TryGetProperty("content", out JsonElement contentElement) &&
                        contentElement.TryGetProperty("parts", out JsonElement parts) &&
                        parts.GetArrayLength() > 0)
                    {
                        JsonElement firstPart = parts[0];
                        if (firstPart.TryGetProperty("text", out JsonElement textElement))
                        {
                            string answer = textElement.GetString() ?? string.Empty;

                            _logger.LogInformation(
                                "Gemini API response received from {Model}. Length: {Length} chars",
                                modelName, answer.Length);

                            return answer;
                        }
                    }
                }

                throw new Exception($"Invalid response format from Gemini API (model: {modelName})");
            }
            catch (HttpRequestException ex) when (IsRateLimitError(ex))
            {
                // Re-throw rate limit errors for fallback handling
                throw;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error calling Gemini API with model: {Model}", modelName);
                throw new Exception($"L?i khi g?i Gemini API (model: {modelName}): {ex.Message}", ex);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Gemini API request timeout for model: {Model}", modelName);
                throw new Exception($"Gemini API request timeout (model: {modelName})", ex);
            }
            catch (Exception ex) when (!(ex is HttpRequestException))
            {
                _logger.LogError(ex, "Unexpected error calling Gemini API with model: {Model}", modelName);
                throw;
            }
        }

        /// <summary>
        /// Checks if the error is a rate limit error (429)
        /// </summary>
        private bool IsRateLimitError(HttpRequestException ex)
        {
            string message = ex.Message.ToLower();
            return message.Contains("429") || message.Contains("rate limit");
        }
    }
}
