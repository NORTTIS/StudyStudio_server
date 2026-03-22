using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.AI.Tools.Interfaces;

namespace StudioStudio_Server.Services.AI.Tools;

[DebuggerStepThrough]
public class GetStudioGroupsTool : IAITool
{
    private readonly IStudioRepository _studioRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IGroupParticipantRepository _participantRepository;
    private readonly ILogger<GetStudioGroupsTool> _logger;

    public string Name => "get_studio_groups";
    public string Description => "Lay danh sach tat ca cac nhom trong Studio. Parameters: studio_id (required), include_stats (optional, default true)";
    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["studio_id"] = new JsonObject { ["type"] = "string" },
            ["include_stats"] = new JsonObject { ["type"] = "boolean" }
        },
        ["required"] = new JsonArray { "studio_id" }
    };

    public GetStudioGroupsTool(
        IStudioRepository studioRepository,
        ITaskRepository taskRepository,
        IGroupParticipantRepository participantRepository,
        ILogger<GetStudioGroupsTool> logger)
    {
        _studioRepository = studioRepository;
        _taskRepository = taskRepository;
        _participantRepository = participantRepository;
        _logger = logger;
    }

    private static string? Js(JsonNode? n) => n?.GetValue<string>();

    public bool ValidateParameters(JsonObject p) =>
        Guid.TryParse(Js(p["studio_id"]), out _);

    public async Task<AIQueryResult> ExecuteAsync(AIQueryContext context, JsonObject parameters, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (!Guid.TryParse(Js(parameters["studio_id"]), out var studioId))
                return AIQueryResult.Error("Invalid studio_id");

            if (context.StudioId.HasValue && context.StudioId.Value != studioId)
                return AIQueryResult.Error("Ban khong co quyen truy cap Studio nay");

            var includeStats = parameters["include_stats"]?.GetValue<bool>() ?? true;

            var groups = await _studioRepository.GetGroupsByStudioIdAsync(studioId);

            var groupsArray = new JsonArray();
            foreach (var g in groups)
            {
                var groupJson = new JsonObject
                {
                    ["id"] = g.GroupId.ToString(),
                    ["name"] = g.GroupName ?? "",
                    ["description"] = g.Description ?? "",
                    ["created_at"] = g.CreatedAt.ToString("yyyy-MM-dd")
                };

                var members = await _participantRepository.GetAllByGroupIdAsync(g.GroupId);
                groupJson["member_count"] = members.Count;

                if (includeStats)
                {
                    var taskStats = await _taskRepository.GetGroupTaskStatisticsAsync(g.GroupId);
                    groupJson["task_stats"] = new JsonObject
                    {
                        ["total_tasks"] = taskStats.TotalTasks,
                        ["completed_tasks"] = taskStats.CompletedTasks,
                        ["completion_percentage"] = taskStats.CompletionPercentage,
                        ["pending_tasks"] = taskStats.TotalTasks - taskStats.CompletedTasks,
                        ["overdue_tasks"] = taskStats.OverdueTasks
                    };
                }

                groupsArray.Add(groupJson);
            }

            sw.Stop();
            return AIQueryResult.Success(new JsonObject
            {
                ["studio_id"] = studioId.ToString(),
                ["total_groups"] = groups.Count,
                ["groups"] = groupsArray,
                ["generated_at"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetStudioGroupsTool error");
            return AIQueryResult.Error("Da xay ra loi khi lay danh sach nhom");
        }
    }
}
