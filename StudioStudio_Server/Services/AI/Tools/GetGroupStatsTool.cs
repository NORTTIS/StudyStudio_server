using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.AI.Tools.Interfaces;

namespace StudioStudio_Server.Services.AI.Tools;

public class GetGroupStatsTool : IAITool
{
    private readonly ITaskRepository _taskRepository;
    private readonly IGroupParticipantRepository _participantRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly ILogger<GetGroupStatsTool> _logger;

    public string Name => "get_group_stats";
    public string Description => "Lay thong ke nhom";
    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["group_id"] = new JsonObject { ["type"] = "string" }
        },
        ["required"] = new JsonArray { "group_id" }
    };

    public GetGroupStatsTool(
        ITaskRepository taskRepository,
        IGroupParticipantRepository participantRepository,
        IGroupRepository groupRepository,
        ILogger<GetGroupStatsTool> logger)
    {
        _taskRepository = taskRepository;
        _participantRepository = participantRepository;
        _groupRepository = groupRepository;
        _logger = logger;
    }

    private static string? Js(JsonNode? n) => n?.GetValue<string>();

    public bool ValidateParameters(JsonObject p) =>
        Guid.TryParse(Js(p["group_id"]), out _);

    public async Task<AIQueryResult> ExecuteAsync(AIQueryContext context, JsonObject parameters, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (!Guid.TryParse(Js(parameters["group_id"]), out var groupId))
                return AIQueryResult.Error("Invalid group_id");

            if (!await _participantRepository.IsUserInGroupAsync(groupId, context.UserId))
                return AIQueryResult.Error("Ban khong co quyen");

            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
                return AIQueryResult.Error("Khong tim thay nhom");

            var taskSummary = await _taskRepository.GetGroupTaskStatisticsAsync(groupId);
            var members = await _participantRepository.GetAllByGroupIdAsync(groupId);

            sw.Stop();
            return AIQueryResult.Success(new JsonObject
            {
                ["group_info"] = new JsonObject
                {
                    ["id"] = group.GroupId.ToString(),
                    ["name"] = group.GroupName ?? "",
                    ["member_count"] = members.Count,
                    ["created_at"] = group.CreatedAt.ToString("yyyy-MM-dd")
                },
                ["task_statistics"] = new JsonObject
                {
                    ["total_tasks"] = taskSummary.TotalTasks,
                    ["completed_tasks"] = taskSummary.CompletedTasks,
                    ["completion_percentage"] = taskSummary.CompletionPercentage,
                    ["pending_tasks"] = taskSummary.TotalTasks - taskSummary.CompletedTasks,
                    ["overdue_tasks"] = taskSummary.OverdueTasks,
                    ["nearest_deadline"] = taskSummary.NearestDeadline?.ToString("yyyy-MM-dd HH:mm")
                },
                ["generated_at"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetGroupStatsTool error");
            return AIQueryResult.Error("Da xay ra loi");
        }
    }
}
