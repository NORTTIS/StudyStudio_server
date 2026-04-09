namespace StudioStudio_Server.Models.Entities
{
    /// <summary>
    /// Tracks AI API usage for rate limiting and analytics.
    /// Token counts are extracted from Gemini API usage_metadata for accurate billing.
    /// </summary>
    public class AIRequestLog
    {
        public Guid RequestId { get; set; }

        public Guid UserId { get; set; }

        /// <summary>
        /// Total tokens used (Input + Output + Cached). Legacy field, kept for backward compatibility.
        /// Use InputTokens, OutputTokens, CachedTokens for detailed tracking.
        /// </summary>
        public int TokenUsed { get; set; }

        /// <summary>
        /// Tokens in the input prompt (user message + conversation history).
        /// Extracted from Gemini usage_metadata.promptTokenCount.
        /// </summary>
        public int InputTokens { get; set; }

        /// <summary>
        /// Tokens in the AI response. Extracted from Gemini usage_metadata.candidatesTokenCount.
        /// </summary>
        public int OutputTokens { get; set; }

        /// <summary>
        /// Tokens from cached context (system prompt + tool descriptions).
        /// Extracted from Gemini usage_metadata.cachedContentTokenCount.
        /// </summary>
        public int CachedTokens { get; set; }

        /// <summary>
        /// Tokens used for internal reasoning (Gemini thinking mode).
        /// Extracted from Gemini usage_metadata.thoughtsTokenCount.
        /// </summary>
        public int ThinkingTokens { get; set; }

        /// <summary>
        /// Number of tool calls made in this AI request (0 = no tools, 1+ = tools called).
        /// </summary>
        public int ToolCallCount { get; set; }

        /// <summary>
        /// Processing time in milliseconds for this request.
        /// </summary>
        public long ProcessingTimeMs { get; set; }

        /// <summary>
        /// Which AI layer was used: Personal, Group, or Master.
        /// </summary>
        public string? AILayer { get; set; }

        /// <summary>
        /// Target context: GroupId or StudioId depending on AILayer.
        /// </summary>
        public Guid? ContextId { get; set; }

        public DateTime CreatedAt { get; set; }

        // Navigation properties
        public virtual User? User { get; set; }
    }
}