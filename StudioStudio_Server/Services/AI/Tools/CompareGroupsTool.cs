using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Interfaces;
using StudioStudio_Server.Services.AI.Models;

namespace StudioStudio_Server.Services.AI.Tools;

[DebuggerStepThrough]
public class CompareGroupsTool(
    IStudioRepository studioRepository,
    ITaskRepository taskRepository,
    IGroupParticipantRepository participantRepository,
    ILogger<CompareGroupsTool> logger) : IAITool
{
    public string Name => "compare_groups";
    public string Description => "So sanh hieu suat giua cac nhom. Parameters: studio_id (required), group_ids (optional array of guids), metrics (optional array: completion_rate/velocity/overdue_count/active_members)";
    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["studio_id"] = new JsonObject { ["type"] = "string" },
            ["group_ids"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } },
            ["metrics"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } }
        },
        ["required"] = new JsonArray { "studio_id" }
    };

    private static string? Js(JsonNode? n) => n?.GetValue<string>();
    private static int Ji(JsonNode? n) => n?.GetValue<int>() ?? 0;

    public bool ValidateParameters(JsonObject p) => true;

    public async Task<AIQueryResult> ExecuteAsync(AIQueryContext context, JsonObject parameters, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (!context.StudioId.HasValue)
                return AIQueryResult.Error("Khong co studio_id trong context");

            var studioId = context.StudioId.Value;

            var studio = await studioRepository.GetByIdAsync(studioId);
            if (studio == null)
                return AIQueryResult.Error("Khong tim thay studio");

            List<Guid> groupIds;
            if (parameters["group_ids"] is JsonArray groupIdsArray && groupIdsArray.Count > 0)
            {
                groupIds = new List<Guid>();
                foreach (var item in groupIdsArray)
                {
                    if (Guid.TryParse(Js(item), out var gid))
                        groupIds.Add(gid);
                }
            }
            else
            {
                var groups = await studioRepository.GetGroupsByStudioIdAsync(studioId);
                groupIds = groups.Select(g => g.GroupId).ToList();
            }

            var defaultMetrics = new[] { "completion_rate", "velocity", "overdue_count", "active_members" };
            var activeMetrics = parameters["metrics"] is JsonArray metricsArray && metricsArray.Count > 0
                ? metricsArray.Select(m => Js(m) ?? "").Where(m => !string.IsNullOrEmpty(m)).ToArray()
                : defaultMetrics;

            var groupMetricsList = new List<JsonObject>();
            foreach (var groupId in groupIds)
            {
                var group = await studioRepository.GetGroupsByStudioIdAsync(studioId)
                    .ContinueWith(t => t.Result.FirstOrDefault(g => g.GroupId == groupId));
                var taskStats = await taskRepository.GetGroupTaskStatisticsAsync(groupId);
                var members = await participantRepository.GetAllByGroupIdAsync(groupId);

                var totalTasks = taskStats.TotalTasks;
                var completedTasks = taskStats.CompletedTasks;
                var completionRate = totalTasks > 0 ? Math.Round((double)completedTasks / totalTasks * 100, 1) : 0.0;
                var velocity = members.Count > 0 ? Math.Round((double)completedTasks / members.Count, 2) : 0.0;
                var overdueCount = taskStats.OverdueTasks;
                var activeMembers = members.Count;

                groupMetricsList.Add(new JsonObject
                {
                    ["group_id"] = groupId.ToString(),
                    ["group_name"] = group?.GroupName ?? "",
                    ["completion_rate"] = completionRate,
                    ["velocity"] = velocity,
                    ["overdue_count"] = overdueCount,
                    ["active_members"] = activeMembers
                });
            }

            // Rank by first available metric
            var firstMetric = activeMetrics.FirstOrDefault() ?? "completion_rate";
            var ranked = groupMetricsList
                .OrderByDescending(g => Ji(g[firstMetric] ?? g["completion_rate"]))
                .ToList();

            for (int i = 0; i < ranked.Count; i++)
                ranked[i]["rank"] = i + 1;

            var bestPerformer = ranked.FirstOrDefault();
            var needsAttention = ranked.LastOrDefault();

            sw.Stop();
            return AIQueryResult.Success(new JsonObject
            {
                ["studio_id"] = studioId.ToString(),
                ["studio_name"] = studio.StudioName,
                ["comparison_summary"] = $"So sanh {groupMetricsList.Count} nhom theo cac chi tieu: {string.Join(", ", activeMetrics)}",
                ["group_metrics"] = new JsonArray(ranked.ToArray()),
                ["best_performer"] = bestPerformer != null ? new JsonObject
                {
                    ["group_id"] = bestPerformer["group_id"],
                    ["group_name"] = bestPerformer["group_name"],
                    ["rank"] = 1,
                    ["metrics"] = new JsonObject
                    {
                        ["completion_rate"] = bestPerformer["completion_rate"],
                        ["velocity"] = bestPerformer["velocity"],
                        ["overdue_count"] = bestPerformer["overdue_count"],
                        ["active_members"] = bestPerformer["active_members"]
                    }
                } : null!,
                ["needs_attention"] = needsAttention != null && ranked.Count > 1 ? new JsonObject
                {
                    ["group_id"] = needsAttention["group_id"],
                    ["group_name"] = needsAttention["group_name"],
                    ["rank"] = ranked.Count,
                    ["metrics"] = new JsonObject
                    {
                        ["completion_rate"] = needsAttention["completion_rate"],
                        ["velocity"] = needsAttention["velocity"],
                        ["overdue_count"] = needsAttention["overdue_count"],
                        ["active_members"] = needsAttention["active_members"]
                    }
                } : null!,
                ["generated_at"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CompareGroupsTool error");
            return AIQueryResult.Error("Da xay ra loi khi so sanh cac nhom");
        }
    }
}
