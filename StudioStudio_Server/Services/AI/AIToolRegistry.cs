using System.Text.Json;
using System.Text.Json.Nodes;
using StudioStudio_Server.Services.AI.Tools.Interfaces;

namespace StudioStudio_Server.Services.AI;

/// <summary>
/// Registry quản lý tất cả AI Tools
/// Singleton pattern
/// </summary>
public class AIToolRegistry : IAIToolRegistry
{
    private readonly Dictionary<string, IAITool> _tools = new();
    private readonly ILogger<AIToolRegistry> _logger;

    public AIToolRegistry(ILogger<AIToolRegistry> logger)
    {
        _logger = logger;
    }

    public IAITool? GetTool(string name)
    {
        _tools.TryGetValue(name, out var tool);
        return tool;
    }

    public IReadOnlyList<IAITool> GetAllTools()
    {
        return _tools.Values.ToList().AsReadOnly();
    }

    public void RegisterTool(IAITool tool)
    {
        if (_tools.ContainsKey(tool.Name))
        {
            _logger.LogWarning("Tool {ToolName} already registered, skipping", tool.Name);
            return;
        }

        _tools[tool.Name] = tool;
        _logger.LogInformation("Registered AI Tool: {ToolName}", tool.Name);
    }

    public JsonObject GetToolsManifest()
    {
        var tools = new JsonArray();

        foreach (var tool in _tools.Values)
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
