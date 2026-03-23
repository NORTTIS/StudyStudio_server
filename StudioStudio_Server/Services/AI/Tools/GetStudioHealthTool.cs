using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.AI.Tools.Interfaces;

namespace StudioStudio_Server.Services.AI.Tools;

[DebuggerStepThrough]
public class GetStudioHealthTool : IAITool
{
    private readonly IStudioRepository _studioRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IGroupParticipantRepository _participantRepository;
    private readonly IAnalyticsRepository _analyticsRepository;
    private readonly ILogger<GetStudioHealthTool> _logger;

    public string Name => "get_studio_health";
    public string Description => "Kiem tra suc khoe tong the cua Studio. Parameters: studio_id (required)";
    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["studio_id"] = new JsonObject { ["type"] = "string" }
        },
        ["required"] = new JsonArray { "studio_id" }
    };

    public GetStudioHealthTool(
        IStudioRepository studioRepository,
        ITaskRepository taskRepository,
        IGroupParticipantRepository participantRepository,
        IAnalyticsRepository analyticsRepository,
        ILogger<GetStudioHealthTool> logger)
    {
        _studioRepository = studioRepository;
        _taskRepository = taskRepository;
        _participantRepository = participantRepository;
        _analyticsRepository = analyticsRepository;
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
            // Auto-inject studio_id from context if LLM didn't provide it
            if (!parameters.ContainsKey("studio_id") && context.StudioId.HasValue)
            {
                parameters["studio_id"] = JsonValue.Create(context.StudioId.Value.ToString());
            }

            if (!Guid.TryParse(Js(parameters["studio_id"]), out var studioId))
                return AIQueryResult.Error("Invalid studio_id");

            if (context.StudioId != studioId)
                return AIQueryResult.Error("Ban khong co quyen truy cap studio nay");

            var studio = await _studioRepository.GetByIdAsync(studioId);
            if (studio == null)
                return AIQueryResult.Error("Khong tim thay studio");

            var groups = await _studioRepository.GetGroupsByStudioIdAsync(studioId);
            if (groups.Count == 0)
                return AIQueryResult.Error("Studio khong co nhom nao");

            var contributingFactors = new List<string>();
            var topRisks = new List<JsonObject>();

            var groupStats = new List<(Guid groupId, string name, int total, int completed, int overdue, int members)>();
            foreach (var group in groups)
            {
                var taskStats = await _taskRepository.GetGroupTaskStatisticsAsync(group.GroupId);
                var members = await _participantRepository.GetAllByGroupIdAsync(group.GroupId);
                groupStats.Add((group.GroupId, group.GroupName, taskStats.TotalTasks, taskStats.CompletedTasks, taskStats.OverdueTasks, members.Count));
            }

            var totalTasksAll = groupStats.Sum(g => g.total);
            var totalCompletedAll = groupStats.Sum(g => g.completed);
            var totalOverdueAll = groupStats.Sum(g => g.overdue);
            var avgMembersActive = groupStats.Count > 0 ? groupStats.Average(g => g.members) : 0;

            var baseScore = 70;
            var completionRateScore = totalTasksAll > 0
                ? (int)Math.Round((double)totalCompletedAll / totalTasksAll * 40)
                : 20;
            var overduePenalty = Math.Max(0, totalOverdueAll * 2);
            var engagementScore = avgMembersActive >= 5 ? 10 : avgMembersActive >= 3 ? 5 : 0;

            var healthScore = Math.Min(100, Math.Max(0, baseScore + completionRateScore - overduePenalty + engagementScore));

            if (completionRateScore >= 35) contributingFactors.Add("Ty le hoan thanh tot");
            if (engagementScore >= 8) contributingFactors.Add("Thanh vien hoat dong nhieu");
            if (totalOverdueAll > 5) contributingFactors.Add("So luong cong viec qua han nhieu");

            var healthStatus = healthScore >= 85 ? "excellent" : healthScore >= 70 ? "good" : healthScore >= 50 ? "warning" : "critical";

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
                ["health_score"] = healthScore,
                ["health_status"] = healthStatus,
                ["contributing_factors"] = new JsonArray(contributingFactors.Select(f => JsonValue.Create(f)).ToArray()),
                ["top_risks"] = new JsonArray(topRisks.ToArray()),
                ["summary"] = $"Studio '{studio.StudioName}' co diem suc khoe {healthScore}/100 - trang thai: {healthStatus}. " +
                              $"Tong {totalTasksAll} cong viec, {totalCompletedAll} hoan thanh, {totalOverdueAll} qua han."
            }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetStudioHealthTool error");
            return AIQueryResult.Error("Da xay ra loi khi kiem tra suc khoe studio");
        }
    }
}
