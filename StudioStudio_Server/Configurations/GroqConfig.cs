namespace StudioStudio_Server.Configurations
{
    /// <summary>
    /// C?u h?nh cho Groq LLM API
    /// Model: llama-3.3-70b-versatile
    /// </summary>
    public class GroqConfig
    {
        /// <summary>
        /// Groq API Key
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Model name cho LLM inference
        /// Default: llama-3.3-70b-versatile
        /// </summary>
        public string Model { get; set; } = "llama-3.3-70b-versatile";

        /// <summary>
        /// API endpoint cho Groq
        /// Default: https://api.groq.com/openai/v1
        /// </summary>
        public string Endpoint { get; set; } = "https://api.groq.com/openai/v1";

        /// <summary>
        /// Timeout cho request (giây)
        /// Default: 60 giây
        /// </summary>
        public int TimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// Max tokens cho response
        /// Default: 2000
        /// </summary>
        public int MaxTokens { get; set; } = 2000;

        /// <summary>
        /// Temperature cho response (0.0 - 2.0)
        /// Cao = creative, Th?p = focused
        /// Default: 0.7
        /// </summary>
        public double Temperature { get; set; } = 0.7;

        /// <summary>
        /// Top P sampling (0.0 - 1.0)
        /// Default: 0.9
        /// </summary>
        public double TopP { get; set; } = 0.9;
    }
}
