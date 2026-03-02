using Microsoft.Extensions.Options;
using StudioStudio_Server.Configurations;
using StudioStudio_Server.Services.Interfaces;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service implementation cho Groq LLM API
    /// S? d?ng llama-3.3-70b-versatile model
    /// </summary>
    public class GroqLLMService : ILLMService
    {
        private readonly HttpClient _httpClient;
        private readonly GroqConfig _config;
        private readonly ILogger<GroqLLMService> _logger;

        public GroqLLMService(
            HttpClient httpClient,
            IOptions<GroqConfig> config,
            ILogger<GroqLLMService> logger)
        {
            _httpClient = httpClient;
            _config = config.Value;
            _logger = logger;

            // C?u h?nh HTTP client
            _httpClient.BaseAddress = new Uri(_config.Endpoint);
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _config.ApiKey);
            _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
        }

        /// <summary>
        /// G?i Groq API ð? generate câu tr? l?i t? context và câu h?i
        /// </summary>
        public async Task<string> GenerateAnswerAsync(
            string systemPrompt,
            string userMessage,
            string context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                // Combine context v?i user message
                string fullMessage = $"{context}\n\nCâu h?i: {userMessage}";

                // Build request body theo OpenAI-compatible format
                var requestBody = new
                {
                    model = _config.Model,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = fullMessage }
                    },
                    temperature = _config.Temperature,
                    max_tokens = _config.MaxTokens,
                    top_p = _config.TopP
                };

                string jsonRequest = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                _logger.LogInformation("Calling Groq API with model: {Model}", _config.Model);

                // G?i API
                HttpResponseMessage response = await _httpClient.PostAsync(
                    "chat/completions",
                    content,
                    cancellationToken);

                response.EnsureSuccessStatusCode();

                string jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);

                // Parse response
                using JsonDocument doc = JsonDocument.Parse(jsonResponse);
                JsonElement root = doc.RootElement;

                if (root.TryGetProperty("choices", out JsonElement choices) &&
                    choices.GetArrayLength() > 0)
                {
                    JsonElement firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("message", out JsonElement message) &&
                        message.TryGetProperty("content", out JsonElement contentProp))
                    {
                        string answer = contentProp.GetString() ?? string.Empty;

                        _logger.LogInformation("Groq API response received. Length: {Length} chars",
                            answer.Length);

                        return answer;
                    }
                }

                throw new Exception("Invalid response format from Groq API");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error calling Groq API");
                throw new Exception($"L?i khi g?i Groq API: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Groq API request timeout");
                throw new Exception("Groq API request timeout", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error calling Groq API");
                throw;
            }
        }
    }
}
