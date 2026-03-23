using System.Text.Json;
using System.Text.Json.Nodes;

namespace StudioStudio_Server.Services.AI.Models;

/// <summary>
/// Kết quả trả về từ AI Tool
/// </summary>
public class AIQueryResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public JsonObject? Data { get; set; }
    public long ExecutionTimeMs { get; set; }

    public static AIQueryResult Success(JsonObject data, long executionTimeMs = 0)
    {
        return new AIQueryResult
        {
            IsSuccess = true,
            Data = data,
            ExecutionTimeMs = executionTimeMs
        };
    }

    public static AIQueryResult Error(string message)
    {
        return new AIQueryResult
        {
            IsSuccess = false,
            ErrorMessage = message
        };
    }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}

/// <summary>
/// Context chung cho tất cả tools
/// </summary>
public class AIQueryContext
{
    /// <summary>
    /// User ID đang hỏi
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Ngôn ngữ của user (vi/en)
    /// </summary>
    public string Language { get; set; } = "vi";

    /// <summary>
    /// Group ID hiện tại (nếu có)
    /// </summary>
    public Guid? GroupId { get; set; }

    /// <summary>
    /// Studio ID hiện tại (nếu có)
    /// </summary>
    public Guid? StudioId { get; set; }

    /// <summary>
    /// User's subscription plan
    /// </summary>
    public string SubscriptionPlan { get; set; } = "Free";

    /// <summary>
    /// Timestamp khi bắt đầu query
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Lịch sử tool calls trong một query
/// </summary>
public class ToolExecutionHistory
{
    public List<ToolCallEntry> Calls { get; } = new();

    public void AddCall(string toolName, JsonObject parameters, AIQueryResult result)
    {
        Calls.Add(new ToolCallEntry
        {
            ToolName = toolName,
            Parameters = parameters,
            Result = result,
            ExecutedAt = DateTime.UtcNow
        });
    }

    public string GetSummary()
    {
        if (Calls.Count == 0) return "";

        var lines = new List<string> { "Tool calls:" };
        foreach (var call in Calls)
        {
            lines.Add($"- {call.ToolName}: {(call.Result.IsSuccess ? "OK" : "Error")}");
        }
        return string.Join("\n", lines);
    }
}

public class ToolCallEntry
{
    public string ToolName { get; set; } = "";
    public JsonObject Parameters { get; set; } = new();
    public AIQueryResult Result { get; set; } = new();
    public DateTime ExecutedAt { get; set; }
}
