using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using StudioStudio_Server.Configurations;
using StudioStudio_Server.Services.Interfaces;
using System.Runtime.CompilerServices;
using System.Text;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service implementation for Gemini LLM API with automatic fallback
    /// Primary: gemini-2.5-flash (fast and efficient)
    /// Fallback: gemini-2.5-pro (more capable, when rate limited or primary fails)
    /// </summary>
    public class GeminiLLMService : ILLMService
    {
        private readonly HttpClient _httpClient;
        private readonly GeminiConfig _config;
        private readonly ILogger<GeminiLLMService> _logger;
        private const string GEMINI_ENDPOINT = "https://generativelanguage.googleapis.com/v1beta/models";
        private const string PRIMARY_MODEL = "gemini-2.5-flash";
        private const string FALLBACK_MODEL = "gemini-2.5-pro";

        // JSON Schema for structured output
        private static readonly JsonObject AgentResponseSchema = new()
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["action"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray { JsonValue.Create("tool_call"), JsonValue.Create("answer") }
                },
                ["tool_name"] = new JsonObject { ["type"] = "string" },
                ["parameters"] = new JsonObject { ["type"] = "object" },
                ["final_answer"] = new JsonObject { ["type"] = "string" }
            },
            ["required"] = new JsonArray { JsonValue.Create("action") }
        };

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
        /// 2. If 429 (rate limit), fallback to FALLBACK_MODEL (gemini-2.5-pro)
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

                // Fallback to alternative model
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
                string fullMessage = $"{context}\n\nQuestion: {userMessage}";

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
                        responseMimeType = "application/json",
                        responseSchema = AgentResponseSchema
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
                throw new Exception($"Error calling Gemini API (model: {modelName}): {ex.Message}", ex);
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
        /// Generates answer using Gemini LLM with automatic fallback on rate limit (streaming version)
        /// Flow:
        /// 1. Try PRIMARY_MODEL (gemini-2.5-flash)
        /// 2. If 429 (rate limit) before first chunk, fallback to FALLBACK_MODEL (gemini-2.5-pro)
        /// 3. Stream response chunks as they arrive
        /// Note: Fallback only works if error occurs before first chunk is yielded
        /// </summary>
        public async IAsyncEnumerable<string> GenerateAnswerStreamAsync(
            string systemPrompt,
            string userMessage,
            string context,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Validate API key
            if (string.IsNullOrEmpty(_config.ApiKey))
            {
                _logger.LogError("Gemini API Key not configured. Cannot generate answer.");
                throw new InvalidOperationException("Gemini API Key is not configured");
            }

            _logger.LogInformation("Attempting streaming answer generation with PRIMARY model: {Model}", PRIMARY_MODEL);
            
            // First, try to get the first chunk from primary model to detect early failures
            IAsyncEnumerable<string> primaryStream = GenerateAnswerStreamInternalAsync(
                PRIMARY_MODEL,
                systemPrompt,
                userMessage,
                context,
                cancellationToken);

            IAsyncEnumerator<string> primaryEnumerator = primaryStream.GetAsyncEnumerator(cancellationToken);
            
            bool gotFirstChunk = false;
            string? firstChunk = null;
            bool useFallback = false;

            // Try to get first chunk - this will detect connection/rate limit errors early
            try
            {
                gotFirstChunk = await primaryEnumerator.MoveNextAsync();
                if (gotFirstChunk)
                {
                    firstChunk = primaryEnumerator.Current;
                }
            }
            catch (HttpRequestException ex) when (IsRateLimitError(ex))
            {
                await primaryEnumerator.DisposeAsync();
                useFallback = true;
                
                _logger.LogWarning(
                    "Rate limit hit on PRIMARY model ({Model}). Falling back to FALLBACK model ({Fallback})",
                    PRIMARY_MODEL, FALLBACK_MODEL);
            }
            catch (Exception ex)
            {
                await primaryEnumerator.DisposeAsync();
                _logger.LogError(ex, "Error with PRIMARY model ({Model})", PRIMARY_MODEL);
                throw;
            }

            // If we need to use fallback, do it outside of catch block
            if (useFallback)
            {
                IAsyncEnumerable<string> fallbackStream = GenerateAnswerStreamInternalAsync(
                    FALLBACK_MODEL,
                    systemPrompt,
                    userMessage,
                    context,
                    cancellationToken);

                await foreach (var chunk in fallbackStream.WithCancellation(cancellationToken))
                {
                    yield return chunk;
                }
                
                yield break;
            }

            // If we got first chunk successfully, yield it and continue with primary model
            if (gotFirstChunk && firstChunk != null)
            {
                yield return firstChunk;

                // Continue with remaining chunks from primary model
                while (await primaryEnumerator.MoveNextAsync())
                {
                    yield return primaryEnumerator.Current;
                }

                await primaryEnumerator.DisposeAsync();
            }
        }

        /// <summary>
        /// Internal method to call Gemini API with specified model (streaming version)
        /// </summary>
        private async IAsyncEnumerable<string> GenerateAnswerStreamInternalAsync(
            string modelName,
            string systemPrompt,
            string userMessage,
            string context,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // Combine context with user message
            string fullMessage = $"{context}\n\nQuestion: {userMessage}";

            // Build request URL with streaming
            string url = $"{GEMINI_ENDPOINT}/{modelName}:streamGenerateContent?key={_config.ApiKey}&alt=sse";

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
                    responseMimeType = "application/json",
                    responseSchema = AgentResponseSchema
                }
            };

            string jsonRequest = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            _logger.LogInformation("Calling Gemini Streaming API with model: {Model}", modelName);

            // Call API
            HttpResponseMessage response = await _httpClient.PostAsync(url, content, cancellationToken);

            // Handle error responses
            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

                _logger.LogError(
                    "Gemini Streaming API error. Status: {Status}, Model: {Model}, Error: {Error}",
                    response.StatusCode, modelName, errorContent);

                if ((int)response.StatusCode == 429)
                {
                    throw new HttpRequestException($"Rate limit error (429) for model {modelName}");
                }

                throw new Exception($"Gemini Streaming API returned error: {response.StatusCode} - {errorContent}");
            }

            // Read stream
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // SSE format: "data: {...}"
                if (line.StartsWith("data: "))
                {
                    string jsonData = line.Substring(6);

                    using JsonDocument doc = JsonDocument.Parse(jsonData);
                    JsonElement root = doc.RootElement;

                    // Extract text from response
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
                                string chunk = textElement.GetString() ?? string.Empty;
                                if (!string.IsNullOrEmpty(chunk))
                                {
                                    yield return chunk;
                                }
                            }
                        }
                    }
                }
            }

            _logger.LogInformation("Gemini Streaming API response completed from {Model}", modelName);
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
