using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using StudioStudio_Server.Configurations;
using StudioStudio_Server.Services.Interfaces;
using System.Runtime.CompilerServices;
using System.Text;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using StudioStudio_Server.Services.AI.Interfaces;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service implementation for Gemini LLM API with automatic fallback
    /// Primary: gemini-2.5-flash (fast and efficient)
    /// Fallback: gemini-2.5-pro (more capable, when rate limited or primary fails)
    /// Includes Polly resilience policies for circuit breaker and retry.
    /// </summary>
    public class GeminiLLMService : ILLMService
    {
        private readonly HttpClient _httpClient;
        private readonly GeminiConfig _config;
        private readonly ILogger<GeminiLLMService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private const string GEMINI_ENDPOINT = "https://generativelanguage.googleapis.com/v1beta/models";
        private const string PRIMARY_MODEL = "gemini-2.5-flash";
        private const string FALLBACK_MODEL = "gemini-2.5-pro";

        // Polly resilience pipeline for circuit breaker + retry
        private readonly ResiliencePipeline<HttpResponseMessage> _resiliencePipeline;

        // Context caching is disabled because the current Gemini model/API key does not support it reliably.

        public GeminiLLMService(
            HttpClient httpClient,
            IOptions<GeminiConfig> config,
            ILogger<GeminiLLMService> logger,
            IServiceProvider serviceProvider)
        {
            _httpClient = httpClient;
            _config = config.Value;
            _logger = logger;
            _serviceProvider = serviceProvider;

            // Configure HTTP client with LLM-specific timeout
            _httpClient.Timeout = TimeSpan.FromSeconds(_config.LLMTimeoutSeconds);

            // Build Polly resilience pipeline: Retry (3x with exponential backoff) + Circuit Breaker
            _resiliencePipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
                // Retry 3 times with exponential backoff (2s, 4s, 8s) for transient errors
                .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
                {
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>()
                        .Handle<TaskCanceledException>()
                        .HandleResult(r => (int)r.StatusCode >= 500 || (int)r.StatusCode == 429),
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromSeconds(2),
                    BackoffType = DelayBackoffType.Exponential,
                    OnRetry = args =>
                    {
                        _logger.LogWarning(
                            "Gemini API retry attempt {AttemptNumber} after {Delay}ms. Error: {Exception}",
                            args.AttemptNumber, args.RetryDelay.TotalMilliseconds, args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());
                        return ValueTask.CompletedTask;
                    }
                })
                // Circuit breaker: open after 5 failures, stay open for 30s, then half-open
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
                {
                    FailureRatio = 0.5,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    MinimumThroughput = 5,
                    BreakDuration = TimeSpan.FromSeconds(30),
                    OnOpened = args =>
                    {
                        _logger.LogWarning("Gemini API circuit breaker OPENED. Will retry after {Duration}s", args.BreakDuration.TotalSeconds);
                        return ValueTask.CompletedTask;
                    },
                    OnClosed = _ =>
                    {
                        _logger.LogInformation("Gemini API circuit breaker CLOSED. Normal operation resumed.");
                        return ValueTask.CompletedTask;
                    },
                    OnHalfOpened = _ =>
                    {
                        _logger.LogInformation("Gemini API circuit breaker HALF-OPEN. Testing with next request...");
                        return ValueTask.CompletedTask;
                    }
                })
                .Build();

            if (string.IsNullOrEmpty(_config.ApiKey))
            {
                _logger.LogWarning("Gemini API Key not configured. LLM operations will fail at runtime.");
            }
        }

        /// <summary>
        /// Builds request body for streaming without context caching.
        /// Streaming version doesn't use responseSchema (causes JSON issues with streaming).
        /// </summary>
        private object BuildRequestBodyForStreaming(
            string systemPrompt,
            string userMessage,
            bool forceTextMode = false)
        {
            // Base generation config — responseMimeType only set for JSON mode (tool-calling decisions)
            object jsonGenConfig = new
            {
                temperature = _config.Temperature,
                topK = _config.TopK,
                topP = _config.TopP,
                maxOutputTokens = _config.MaxTokens,
                responseMimeType = "application/json"
            };

            object textGenConfig = new
            {
                temperature = _config.Temperature,
                topK = _config.TopK,
                topP = _config.TopP,
                maxOutputTokens = _config.MaxTokens
            };

            return new
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
                            new { text = userMessage }
                        }
                    }
                },
                generationConfig = forceTextMode ? textGenConfig : jsonGenConfig
            };
        }

        /// <summary>
        /// Builds request body without context caching.
        /// </summary>
        private object BuildRequestBody(
            string systemPrompt,
            string userMessage,
            JsonObject? responseSchema,
            string userQuestion)
        {
            _ = userQuestion;
            return new
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
                            new { text = userMessage }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = _config.Temperature,
                    topK = _config.TopK,
                    topP = _config.TopP,
                    maxOutputTokens = _config.MaxTokens,
                    responseMimeType = responseSchema != null ? "application/json" : null,
                    responseSchema
                }
            };
        }

        /// <summary>
        /// Build dynamic JSON Schema with tool_name enum constrained to actual registered tools.
        /// This prevents LLM from hallucinating non-existent tool names.
        /// </summary>
        private JsonObject BuildAgentResponseSchema()
        {
            var toolNames = new JsonArray();

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var registry = scope.ServiceProvider.GetService(typeof(IAIToolRegistry))
                    as IAIToolRegistry;

                if (registry != null)
                {
                    foreach (var tool in registry.GetAllTools())
                    {
                        toolNames.Add(JsonValue.Create(tool.Name));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not get tool names from registry, using fallback");
            }

            if (toolNames.Count == 0)
            {
                _logger.LogWarning("Tool registry returned {Count} tools, using fallback list", toolNames.Count);
                var knownTools = new[] {
                    // Group tools
                    "get_tasks", "get_group_stats", "get_members", "get_deadlines", "search_documents",
                    // Personal tools
                    "get_personal_tasks", "get_personal_deadlines", "get_personal_stats",
                    // Studio/Owner tools
                    "get_studio_groups", "get_studio_analytics", "get_group_comparison",
                    "get_risk_groups", "get_studio_health", "compare_groups"
                };
                foreach (var t in knownTools)
                    toolNames.Add(JsonValue.Create(t));
            }
            else
            {
                _logger.LogInformation("Tool registry returned {Count} tools for response schema", toolNames.Count);
            }

            return new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["action"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JsonArray { JsonValue.Create("tool_call"), JsonValue.Create("answer") }
                    },
                    ["tool_name"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Chi su dung tool_name chinh xac tu danh sach. Khong bat duoc ten moi.",
                        ["enum"] = toolNames
                    },
                    ["parameters"] = new JsonObject { ["type"] = "object" },
                    ["final_answer"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Cau tra loi thuan cua AI. Khong dat cau tra loi trong JSON, code block, hay bat ky format dac biet nao. Chi la van ban thuan tuy voi newlines neu can."
                    }
                },
                ["required"] = new JsonArray { JsonValue.Create("action") }
            };
        }

        /// <summary>
        /// Generates answer using Gemini LLM with token usage tracking.
        /// Extracts usage_metadata from Gemini response for accurate billing and analytics.
        /// </summary>
        public async Task<(string Answer, TokenUsage Usage)> GenerateAnswerWithUsageAsync(
            string systemPrompt,
            string userMessage,
            string context,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(_config.ApiKey))
            {
                _logger.LogError("Gemini API Key not configured. Cannot generate answer.");
                throw new InvalidOperationException("Gemini API Key is not configured");
            }

            try
            {
                _logger.LogInformation("Attempting answer generation with token tracking: {Model}", PRIMARY_MODEL);
                return await GenerateAnswerInternalAsync(
                    PRIMARY_MODEL,
                    systemPrompt,
                    userMessage,
                    context,
                    cancellationToken,
                    BuildAgentResponseSchema());
            }
            catch (HttpRequestException ex) when (IsRateLimitError(ex))
            {
                _logger.LogWarning(
                    "Rate limit hit on PRIMARY model ({Model}). Falling back to FALLBACK model ({Fallback})",
                    PRIMARY_MODEL, FALLBACK_MODEL);

                try
                {
                    return await GenerateAnswerInternalAsync(
                        FALLBACK_MODEL,
                        systemPrompt,
                        userMessage,
                        context,
                        cancellationToken,
                        BuildAgentResponseSchema());
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(
                        fallbackEx,
                        "Fallback model also failed. Primary={Primary}, Fallback={Fallback}",
                        PRIMARY_MODEL,
                        FALLBACK_MODEL);

                    throw new Exception(
                        $"Both PRIMARY ({PRIMARY_MODEL}) and FALLBACK ({FALLBACK_MODEL}) models failed. "
                        + $"Primary error: {ex.Message}. Fallback error: {fallbackEx.Message}",
                        fallbackEx);
                }
            }
        }

        /// <summary>
        /// Generates a raw text response without forcing the agent response schema.
        /// </summary>
        public async Task<string> GenerateTextResponseAsync(
            string systemPrompt,
            string userMessage,
            string context,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(_config.ApiKey))
            {
                _logger.LogError("Gemini API Key not configured. Cannot generate text response.");
                throw new InvalidOperationException("Gemini API Key is not configured");
            }

            try
            {
                _logger.LogInformation("Attempting raw text generation with PRIMARY model: {Model}", PRIMARY_MODEL);
                var result = await GenerateAnswerInternalAsync(
                    PRIMARY_MODEL,
                    systemPrompt,
                    userMessage,
                    context,
                    cancellationToken);
                return result.Answer;
            }
            catch (HttpRequestException ex) when (IsRateLimitError(ex))
            {
                _logger.LogWarning(
                    "Rate limit hit on PRIMARY model ({Model}) for raw text. Falling back to FALLBACK model ({Fallback})",
                    PRIMARY_MODEL, FALLBACK_MODEL);

                var result = await GenerateAnswerInternalAsync(
                    FALLBACK_MODEL,
                    systemPrompt,
                    userMessage,
                    context,
                    cancellationToken);
                return result.Answer;
            }
        }

        /// <summary>
        /// Internal method to call Gemini API with specified model.
        /// Returns both the answer and token usage extracted from usage_metadata.
        /// </summary>
        private async Task<(string Answer, TokenUsage Usage)> GenerateAnswerInternalAsync(
            string modelName,
            string systemPrompt,
            string userMessage,
            string context,
            CancellationToken cancellationToken,
            JsonObject? responseSchema = null)
        {
            try
            {
                // Combine context with user message
                string fullMessage = $"{context}\n\nQuestion: {userMessage}";

                // Build request URL
                string url = $"{GEMINI_ENDPOINT}/{modelName}:generateContent?key={_config.ApiKey}";

                // Build request body according to Gemini API format
                var requestBody = BuildRequestBody(
                    systemPrompt,
                    fullMessage,
                    responseSchema,
                    userMessage);

                string jsonRequest = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                _logger.LogInformation("Calling Gemini API with model: {Model}", modelName);

                // Call API with Polly resilience pipeline (retry + circuit breaker)
                HttpResponseMessage response = await _resiliencePipeline.ExecuteAsync(
                    async token =>
                    {
                        var request = new HttpRequestMessage(HttpMethod.Post, url)
                        {
                            Content = content
                        };
                        return await _httpClient.SendAsync(request, token);
                    },
                    cancellationToken);

                // Handle error responses
                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);

                    _logger.LogError(
                        "Gemini API error. Status: {Status}, Model: {Model}, Error: {Error}",
                        response.StatusCode, modelName, errorContent);

                    // Throw HttpRequestException for retryable errors (will be caught for fallback)
                    // 429 = rate limit, 402 = billing error (try fallback model)
                    if ((int)response.StatusCode == 429 || (int)response.StatusCode == 402)
                    {
                        throw new HttpRequestException($"Retryable error ({response.StatusCode}) for model {modelName}");
                    }

                    // Extract clean error message for non-retryable errors (402, 400, 401, 403, 404, 422)
                    string cleanMessage = ExtractCleanErrorMessage(errorContent);
                    throw new Exception($"Gemini API error ({response.StatusCode}): {cleanMessage}");
                }

                // Parse response
                string jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);

                using JsonDocument doc = JsonDocument.Parse(jsonResponse);
                JsonElement root = doc.RootElement;

                // Extract token usage from usage_metadata
                TokenUsage tokenUsage = ExtractTokenUsage(root);

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
                                "Gemini API response received from {Model}. Length: {Length} chars, Tokens: In={Input} Out={Output} Cached={Cached}",
                                modelName, answer.Length, tokenUsage.InputTokens, tokenUsage.OutputTokens, tokenUsage.CachedTokens);

                            return (answer, tokenUsage);
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
        /// Extracts token usage from Gemini API usage_metadata.
        /// usage_metadata structure:
        /// - prompt_token_count: input tokens
        /// - candidates_token_count: output tokens
        /// - cached_content_token_count: cached tokens (from context caching)
        /// - thoughts_token_count: thinking tokens (from thinking mode)
        /// </summary>
        private TokenUsage ExtractTokenUsage(JsonElement root)
        {
            try
            {
                if (!root.TryGetProperty("usageMetadata", out JsonElement usageMetadata))
                {
                    _logger.LogDebug("No usage_metadata found in response, using zero tokens");
                    return new TokenUsage(0, 0);
                }

                int inputTokens = usageMetadata.TryGetProperty("promptTokenCount", out JsonElement promptEl)
                    ? promptEl.GetInt32() : 0;
                int outputTokens = usageMetadata.TryGetProperty("candidatesTokenCount", out JsonElement outputEl)
                    ? outputEl.GetInt32() : 0;
                int cachedTokens = usageMetadata.TryGetProperty("cachedContentTokenCount", out JsonElement cachedEl)
                    ? cachedEl.GetInt32() : 0;
                int thinkingTokens = usageMetadata.TryGetProperty("thoughtsTokenCount", out JsonElement thoughtsEl)
                    ? thoughtsEl.GetInt32() : 0;

                return new TokenUsage(inputTokens, outputTokens, cachedTokens, thinkingTokens);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract token usage from response");
                return new TokenUsage(0, 0);
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
            [EnumeratorCancellation] CancellationToken cancellationToken = default,
            bool forceTextMode = false)
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
                cancellationToken,
                forceTextMode);

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
                    cancellationToken,
                    forceTextMode);

                await foreach (var chunk in fallbackStream)
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
            [EnumeratorCancellation] CancellationToken cancellationToken,
            bool forceTextMode = false)
        {
            // Combine context with user message
            string fullMessage = $"{context}\n\nQuestion: {userMessage}";

            // Build request URL with streaming
            string url = $"{GEMINI_ENDPOINT}/{modelName}:streamGenerateContent?key={_config.ApiKey}&alt=sse";

            // Build request body according to Gemini API format
            var requestBody = BuildRequestBodyForStreaming(
                systemPrompt,
                fullMessage,
                forceTextMode);

            string jsonRequest = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            _logger.LogInformation("Calling Gemini Streaming API with model: {Model}", modelName);

            // For streaming, use simple retry without circuit breaker (circuit breaker can cause issues with streaming)
            HttpResponseMessage? response = null;
            int retryCount = 0;
            const int maxRetries = 3;

            while (retryCount < maxRetries)
            {
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = content
                    };
                    response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    break;
                }
                catch (Exception ex) when (retryCount < maxRetries - 1)
                {
                    retryCount++;
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount));
                    _logger.LogWarning("Gemini streaming retry {Retry}/{Max} after {Delay}s. Error: {Error}",
                        retryCount, maxRetries, delay.TotalSeconds, ex.Message);
                    await Task.Delay(delay, cancellationToken);
                }
            }

            // Handle error responses
            if (response == null || !response.IsSuccessStatusCode)
            {
                var statusCode = response?.StatusCode ?? System.Net.HttpStatusCode.ServiceUnavailable;
                string errorContent = response != null
                    ? await response.Content.ReadAsStringAsync(cancellationToken)
                    : "All retries failed";

                _logger.LogError(
                    "Gemini Streaming API error. Status: {Status}, Model: {Model}, Error: {Error}",
                    statusCode, modelName, errorContent);

                if ((int)statusCode == 429 || (int)statusCode == 402)
                {
                    throw new HttpRequestException($"Retryable error ({statusCode}) for model {modelName}");
                }

                // Extract clean error message - don't leak raw JSON
                string cleanMessage = ExtractCleanErrorMessage(errorContent);
                throw new Exception($"Gemini Streaming API error ({(int)statusCode}): {cleanMessage}");
            }

            // Read stream
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync(cancellationToken);
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
        /// Extracts a clean, user-safe error message from Gemini API error responses.
        /// Never expose raw JSON in user-facing messages.
        /// </summary>
        private string ExtractCleanErrorMessage(string rawJson)
        {
            try
            {
                // Parse JSON error body if possible
                using var doc = JsonDocument.Parse(rawJson);
                var root = doc.RootElement;

                // Try "error.message" first (standard Gemini format)
                if (root.TryGetProperty("error", out JsonElement error) &&
                    error.TryGetProperty("message", out JsonElement msg))
                {
                    return msg.GetString() ?? "Unknown error";
                }

                // Try "error.description" (alternative format)
                if (error.TryGetProperty("description", out JsonElement desc))
                {
                    return desc.GetString() ?? "Unknown error";
                }

                // Try "error.status" or top-level "message"
                if (root.TryGetProperty("message", out JsonElement topMsg))
                {
                    return topMsg.GetString() ?? "Unknown error";
                }

                if (root.TryGetProperty("status", out JsonElement status))
                {
                    return status.GetString() ?? "Unknown error";
                }
            }
            catch
            {
                // If JSON parsing fails, strip JSON noise from raw text
            }

            // Fallback: strip any remaining JSON artifacts from the message
            string cleaned = rawJson
                .Replace("\n", " ")
                .Replace("\r", " ")
                .Replace("  ", " ")
                .Trim();

            // Truncate very long error messages
            if (cleaned.Length > 200)
            {
                cleaned = cleaned[..200] + "...";
            }

            return cleaned;
        }

        /// <summary>
        /// Checks if the error is a retryable API error (429 rate limit, 402 billing)
        /// </summary>
        private bool IsRateLimitError(HttpRequestException ex)
        {
            string message = ex.Message.ToLower();
            return message.Contains("429") || message.Contains("402") || message.Contains("rate limit") || message.Contains("billing");
        }
    }
}
