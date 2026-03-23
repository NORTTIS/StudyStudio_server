namespace StudioStudio_Server.Models
{

/// <summary>
/// Response model cho AI endpoints
/// </summary>
public class AIResponse
{
    public bool Success { get; set; }
    public string Answer { get; set; } = "";
    public object? Data { get; set; }
    public string Message { get; set; } = "";
}
}
