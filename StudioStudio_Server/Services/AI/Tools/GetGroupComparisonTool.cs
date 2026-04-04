using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.AI.Tools.Interfaces;

namespace StudioStudio_Server.Services.AI.Tools;

[DebuggerStepThrough]
public class GetGroupComparisonTool : IAITool
{
    private readonly IStudioRepository _studioRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IGroupParticipantRepository _participantRepository;
    private readonly ILogger<GetGroupComparisonTool> _logger;

    public string Name => "get_group_comparison";
    public string Description => "So sanh nhieu nhom voi nhau. Khong can tham so (studio_id tu dong lay tu context).";
    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["group_ids"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } },
            ["metrics"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } }
        },
        ["required"] = new JsonArray()
    };

    public GetGroupComparisonTool(
        IStudioRepository studioRepository,
        ITaskRepository taskRepository,
        IGroupParticipantRepository participantRepository,
        ILogger<GetGroupComparisonTool> logger)
    {
        _studioRepository = studioRepository;
        _taskRepository = taskRepository;
        _participantRepository = participantRepository;
        _logger = logger;
    }

    private static string? Js(JsonNode? n) => n?.GetValue<string>();

    private static List<Guid> ParseGuidArray(JsonNode? node)
    {
        var result = new List<Guid>();
        if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (Guid.TryParse(Js(item), out var g))
                    result.Add(g);
            }
        }
        return result;
    }

    public bool ValidateParameters(JsonObject p) => true;

    public async Task<AIQueryResult> ExecuteAsync(AIQueryContext context, JsonObject parameters, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (!context.StudioId.HasValue)
                return AIQueryResult.Error("Khong co studio_id trong context");

            var studioId = context.StudioId.Value;

            var requestedGroupIds = ParseGuidArray(parameters["group_ids"]);
            var requestedMetrics = ParseStringArray(parameters["metrics"]);

            var allGroups = await _studioRepository.GetGroupsByStudioIdAsync(studioId);

            List<Group> groupsToCompare;
            if (requestedGroupIds.Count > 0)
            {
                // Defense-in-depth: only include active groups from allGroups (already filtered by GetGroupsByStudioIdAsync)
                groupsToCompare = allGroups.Where(g => requestedGroupIds.Contains(g.GroupId)).ToList();
            }
            else
            {
                groupsToCompare = allGroups;
            }

            if (groupsToCompare.Count == 0)
                return AIQueryResult.Error("Khong tim thay nhom nao de so sanh");

            var defaultMetrics = requestedMetrics.Count > 0 ? requestedMetrics : new List<string> { "completion_rate" };
            var rankedBy = defaultMetrics.First();

            var groupDataList = new List<JsonObject>();
            foreach (var g in groupsToCompare)
            {
                var members = await _participantRepository.GetAllByGroupIdAsync(g.GroupId);
                var taskStats = await _taskRepository.GetGroupTaskStatisticsAsync(g.GroupId);
                var completionRate = taskStats.TotalTasks > 0
                    ? Math.Round((double)taskStats.CompletedTasks / taskStats.TotalTasks * 100, 2)
                    : 0.0;

                groupDataList.Add(new JsonObject
                {
                    ["group_id"] = g.GroupId.ToString(),
                    ["group_name"] = g.GroupName ?? "",
                    ["member_count"] = members.Count,
                    ["total_tasks"] = taskStats.TotalTasks,
                    ["completed_tasks"] = taskStats.CompletedTasks,
                    ["pending_tasks"] = taskStats.TotalTasks - taskStats.CompletedTasks,
                    ["completion_rate"] = completionRate,
                    ["overdue_tasks"] = taskStats.OverdueTasks
                });
            }

            // Rank by selected metric
            var rankedList = rankedBy switch
            {
                "completion_rate" => groupDataList.OrderByDescending(x => x["completion_rate"]?.GetValue<double>()).ToList(),
                "overdue" => groupDataList.OrderByDescending(x => x["overdue_tasks"]?.GetValue<int>()).ToList(),
                "activity" => groupDataList.OrderByDescending(x => x["total_tasks"]?.GetValue<int>()).ToList(),
                _ => groupDataList.OrderByDescending(x => x["completion_rate"]?.GetValue<double>()).ToList()
            };

            var groupsArray = new JsonArray();
            for (int i = 0; i < rankedList.Count; i++)
            {
                var item = rankedList[i];
                item["rank"] = i + 1;
                groupsArray.Add(item);
            }

            sw.Stop();
            return AIQueryResult.Success(new JsonObject
            {
                ["studio_id"] = studioId.ToString(),
                ["total_groups"] = groupsToCompare.Count,
                ["ranked_by"] = rankedBy,
                ["groups"] = groupsArray,
                ["generated_at"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetGroupComparisonTool error");
            return AIQueryResult.Error("Da xay ra loi khi so sanh nhom");
        }
    }

    private static List<string> ParseStringArray(JsonNode? node)
    {
        var result = new List<string>();
        if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                var val = Js(item);
                if (!string.IsNullOrEmpty(val))
                    result.Add(val);
            }
        }
        return result;
    }
}
