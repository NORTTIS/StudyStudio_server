using System.Text.Json;
using System.Text.Json.Nodes;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.AI.Tools.Interfaces;

namespace StudioStudio_Server.Services.AI;

/// <summary>
/// Registry quản lý tất cả AI Tools
/// Singleton pattern - nhưng lưu TOOL TYPE chứ không phải instance
/// để resolve fresh instance trong request scope tránh DbContext disposed
/// </summary>
public class AIToolRegistry : IAIToolRegistry
{
    // Lưu tool TYPE để resolve fresh instance (tránh DbContext disposed)
    private readonly Dictionary<string, Type> _toolTypes = new();
    // Giữ instance để generate manifest (manifest chỉ cần metadata, không cần DbContext)
    private readonly Dictionary<string, IAITool> _toolInstances = new();
    private readonly ILogger<AIToolRegistry> _logger;

    // Tool categories for role-based filtering
    private static readonly HashSet<string> PersonalTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "get_personal_tasks", "get_personal_deadlines", "get_personal_stats"
    };

    private static readonly HashSet<string> GroupTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "get_tasks", "get_group_stats", "get_members", "get_deadlines",
        "search_documents", "get_group_performance", "get_group_documents",
        "get_group_risk"
    };

    private static readonly HashSet<string> StudioTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "get_studio_groups", "get_studio_analytics", "get_group_comparison",
        "get_storage_usage", "get_member_permissions", "get_risk_groups",
        "get_studio_health", "compare_groups"
    };

    public AIToolRegistry(ILogger<AIToolRegistry> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Lấy tool instance (dùng cho manifest - không gọi ExecuteAsync ở đây)
    /// </summary>
    public IAITool? GetTool(string name)
    {
        _toolInstances.TryGetValue(name, out var tool);
        return tool;
    }

    /// <summary>
    /// Lấy tool TYPE (dùng để resolve fresh instance trong request scope)
    /// </summary>
    public Type? GetToolType(string name)
    {
        _toolTypes.TryGetValue(name, out var type);
        return type;
    }

    public IReadOnlyList<IAITool> GetAllTools()
    {
        return _toolInstances.Values.ToList().AsReadOnly();
    }

    public void RegisterTool(IAITool tool)
    {
        if (_toolTypes.ContainsKey(tool.Name))
        {
            _logger.LogWarning("Tool {ToolName} already registered, skipping", tool.Name);
            return;
        }

        _toolTypes[tool.Name] = tool.GetType();     // Lưu TYPE để resolve fresh instance
        _toolInstances[tool.Name] = tool;            // Giữ instance cho manifest
        _logger.LogInformation("Registered AI Tool: {ToolName} (Type: {ToolType})", tool.Name, tool.GetType().Name);
    }

    /// <summary>
    /// Lấy tools manifest cho một context cụ thể - chỉ trả về tools phù hợp với role
    /// </summary>
    public JsonObject GetToolsManifestForContext(AIQueryContext context)
    {
        var allowedTools = GetAllowedTools(context);
        var tools = new JsonArray();

        foreach (var tool in allowedTools)
        {
            tools.Add(new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = tool.ParametersSchema
                }
            });
        }

        _logger.LogDebug("Tools manifest for context: StudioId={StudioId}, GroupId={GroupId}, Count={Count}",
            context.StudioId, context.GroupId, tools.Count);

        return new JsonObject
        {
            ["tools"] = tools
        };
    }

    /// <summary>
    /// Lấy danh sách tools được phép sử dụng theo context
    /// </summary>
    public IReadOnlyList<IAITool> GetAllowedTools(AIQueryContext context)
    {
        // Master AI (Studio Owner) - có quyền tất cả tools
        if (context.StudioId.HasValue)
        {
            return _toolInstances.Values.ToList().AsReadOnly();
        }

        // Group AI - chỉ group tools
        if (context.GroupId.HasValue)
        {
            return _toolInstances.Values
                .Where(t => GroupTools.Contains(t.Name))
                .ToList()
                .AsReadOnly();
        }

        // Personal AI - chỉ personal tools
        return _toolInstances.Values
            .Where(t => PersonalTools.Contains(t.Name))
            .ToList()
            .AsReadOnly();
    }

    public JsonObject GetToolsManifest()
    {
        var tools = new JsonArray();

        foreach (var tool in _toolInstances.Values)
        {
            tools.Add(new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = tool.ParametersSchema
                }
            });
        }

        return new JsonObject
        {
            ["tools"] = tools
        };
    }
}
