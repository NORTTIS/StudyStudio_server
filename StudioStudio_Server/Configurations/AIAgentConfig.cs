namespace StudioStudio_Server.Configurations;

/// <summary>
/// Configuration cho AI Agent - token limits và thông số optimization
/// </summary>
public class AIAgentConfig
{
    public const string SectionName = "Gemini:Agent";
    
    /// <summary>
    /// Số lần gọi tool tối đa trước khi LLM phải trả lời
    /// </summary>
    public int MaxToolCalls { get; set; } = 5;
    
    /// <summary>
    /// Giới hạn tokens cho prompt (Gemini limit)
    /// 1 token ≈ 3.5 ký tự (mixed VN/EN/JSON)
    /// </summary>
    public int MaxContextTokens { get; set; } = 10000;
    
    /// <summary>
    /// Hệ số chuyển đổi từ ký tự sang tokens
    /// 1 token ≈ 3.5 ký tự → 1 ký tự = ~0.28 tokens
    /// Tùy vào ngôn ngữ:
    /// - Tiếng Anh: 0.25 (1 token = 4 chars)
    /// - Tiếng Việt: 0.5 (1 token = 2 chars)
    /// - Mixed + JSON: 0.28 (default)
    /// </summary>
    public double TokensPerCharacter { get; set; } = 0.28;
}
