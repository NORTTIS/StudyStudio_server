using System.Diagnostics;
using System.Text.Json.Nodes;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Interfaces;
using StudioStudio_Server.Services.AI.Models;

namespace StudioStudio_Server.Services.AI.Tools;

[DebuggerStepThrough]
public class GetStudioHealthTool(
    IStudioRepository studioRepository,
    ITaskRepository taskRepository,
    IGroupParticipantRepository participantRepository,
    ILogger<GetStudioHealthTool> logger) : IAITool
{

    public string Name => "get_studio_health";
    public string Description => "Kiem tra suc khoe tong the cua Studio. Khong can tham so (studio_id tu dong lay tu context).";
    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject { },
        ["required"] = new JsonArray()
    };

    private static string? Js(JsonNode? n) => n?.GetValue<string>();

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

            var groups = await studioRepository.GetGroupsByStudioIdAsync(studioId);
            if (groups.Count == 0)
                return AIQueryResult.Error("Studio khong co nhom nao");

            var contributingFactors = new List<string>();
            var topRisks = new List<JsonObject>();

            // Batch: all task stats in 1 query
            var groupIds = groups.Select(g => g.GroupId).ToList();
            var taskStatsMap = await taskRepository.GetGroupTaskStatisticsBatchAsync(groupIds);
            var participantCountsMap = await participantRepository.GetParticipantCountsBatchAsync(groupIds);

            var groupStats = new List<(Guid groupId, string name, int total, int completed, int overdue, int members)>();
            foreach (var group in groups)
            {
                if (taskStatsMap.TryGetValue(group.GroupId, out var taskStats))
                {
                    var members = participantCountsMap.TryGetValue(group.GroupId, out var count) ? count : 0;
                    groupStats.Add((group.GroupId, group.GroupName, taskStats.TotalTasks, taskStats.CompletedTasks, taskStats.OverdueTasks, members));
                }
            }

            var totalTasksAll = groupStats.Sum(g => g.total);
            var totalCompletedAll = groupStats.Sum(g => g.completed);
            var totalOverdueAll = groupStats.Sum(g => g.overdue);
            var avgMembersActive = groupStats.Count > 0 ? groupStats.Average(g => g.members) : 0;

            var completionRatePercent = totalTasksAll > 0
               ? Math.Round((double)totalCompletedAll / totalTasksAll * 100, 1)
               : 0.0;

            if (completionRatePercent >= 85) contributingFactors.Add("Ty le hoan thanh tot");
            if (avgMembersActive >= 5) contributingFactors.Add("Thanh vien hoat dong nhieu");
            if (totalOverdueAll > 5) contributingFactors.Add("So luong cong viec qua han nhieu");

            foreach (var gs in groupStats)
            {
                var compRate = gs.total > 0 ? Math.Round((double)gs.completed / gs.total * 100, 1) : 0.0;
                if (compRate < 50 || gs.overdue >= 3)
                {
                    topRisks.Add(new JsonObject
                    {
                        ["group_id"] = gs.groupId.ToString(),
                        ["group_name"] = gs.name,
                        ["completion_rate"] = compRate,
                        ["overdue_tasks"] = gs.overdue
                    });
                }
            }

            sw.Stop();
            return AIQueryResult.Success(new JsonObject
            {
                ["studio_id"] = studioId.ToString(),
                ["studio_name"] = studio.StudioName,
                ["contributing_factors"] = new JsonArray(contributingFactors.Select(f => JsonValue.Create(f)).ToArray()),
                ["top_risks"] = new JsonArray(topRisks.ToArray()),
                ["summary"] = $"Studio '{studio.StudioName}' co tong {totalTasksAll} cong viec, {totalCompletedAll} hoan thanh, {totalOverdueAll} qua han."
            }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetStudioHealthTool error");
            return AIQueryResult.Error("Da xay ra loi khi kiem tra suc khoe studio");
        }
    }
}
