using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.AI.Tools.Interfaces;

namespace StudioStudio_Server.Services.AI.Tools;

[DebuggerStepThrough]
public class GetRiskGroupsTool : IAITool
{
    private readonly IStudioRepository _studioRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IGroupParticipantRepository _participantRepository;
    private readonly IAnalyticsRepository _analyticsRepository;
    private readonly ILogger<GetRiskGroupsTool> _logger;

    public string Name => "get_risk_groups";
    public string Description => "Xac dinh cac nhom co nguy co. Parameters: studio_id (required), threshold (optional, default completion_rate < 60)";
    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["studio_id"] = new JsonObject { ["type"] = "string" },
            ["threshold"] = new JsonObject { ["type"] = "number", ["default"] = 60 }
        },
        ["required"] = new JsonArray { "studio_id" }
    };

    public GetRiskGroupsTool(
        IStudioRepository studioRepository,
        ITaskRepository taskRepository,
        IGroupParticipantRepository participantRepository,
        IAnalyticsRepository analyticsRepository,
        ILogger<GetRiskGroupsTool> logger)
    {
        _studioRepository = studioRepository;
        _taskRepository = taskRepository;
        _participantRepository = participantRepository;
        _analyticsRepository = analyticsRepository;
        _logger = logger;
    }

    private static string? Js(JsonNode? n) => n?.GetValue<string>();
    private static double Jd(JsonNode? n) => n?.GetValue<double>() ?? 60.0;

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

            var threshold = Jd(parameters["threshold"]);
            var groups = await _studioRepository.GetGroupsByStudioIdAsync(studioId);

            var riskGroups = new List<JsonObject>();
            var now = DateTime.UtcNow;
            var sevenDaysAgo = DateOnly.FromDateTime(now.AddDays(-7));

            foreach (var group in groups)
            {
                var taskStats = await _taskRepository.GetGroupTaskStatisticsAsync(group.GroupId);
                var analytics = await _analyticsRepository.GetGroupAnalyticsRangeAsync(group.GroupId, sevenDaysAgo, DateOnly.FromDateTime(now));

                var totalTasks = taskStats.TotalTasks;
                var completedTasks = taskStats.CompletedTasks;
                var overdueTasks = taskStats.OverdueTasks;
                var completionRate = totalTasks > 0 ? Math.Round((double)completedTasks / totalTasks * 100, 1) : 0.0;

                var hasRecentActivity = analytics.Any(a => a.ActiveMembers > 0 || a.MessagesCount > 0 || a.CompletedTasks > 0);
                var isLowCompletion = completionRate < threshold;
                var isHighOverdue = overdueTasks >= 2;
                var isInactive = !hasRecentActivity;

                if (!isLowCompletion && !isHighOverdue && !isInactive)
                    continue;

                var riskFactors = new List<string>();
                if (isLowCompletion) riskFactors.Add($"Ty le hoan thanh thap ({completionRate}%)");
                if (isHighOverdue) riskFactors.Add($"Nhieu cong viec qua han ({overdueTasks})");
                if (isInactive) riskFactors.Add("Khong co hoat dong gan day");

                var riskLevel = (isLowCompletion && isHighOverdue) ? "HIGH" : (isLowCompletion || isHighOverdue) ? "MEDIUM" : "LOW";

                var recommendations = new List<string>();
                if (isLowCompletion) recommendations.Add("Tang toc do hoan thanh cong viec");
                if (isHighOverdue) recommendations.Add("Uu tien xu ly cac cong viec qua han");
                if (isInactive) recommendations.Add("Kich hoat thanh vien bang cach tao cong viec moi hoac cuoc hop");

                riskGroups.Add(new JsonObject
                {
                    ["group_id"] = group.GroupId.ToString(),
                    ["name"] = group.GroupName,
                    ["completion_rate"] = completionRate,
                    ["overdue_tasks"] = overdueTasks,
                    ["risk_level"] = riskLevel,
                    ["risk_factors"] = new JsonArray(riskFactors.Select(f => JsonValue.Create(f)).ToArray()),
                    ["recommendations"] = new JsonArray(recommendations.Select(r => JsonValue.Create(r)).ToArray())
                });
            }

            riskGroups.Sort((a, b) =>
            {
                var order = new Dictionary<string, int> { ["HIGH"] = 0, ["MEDIUM"] = 1, ["LOW"] = 2 };
                return (order.TryGetValue(a["risk_level"]?.GetValue<string>() ?? "", out var ra) ? ra : 2)
                    .CompareTo(order.TryGetValue(b["risk_level"]?.GetValue<string>() ?? "", out var rb) ? rb : 2);
            });

            sw.Stop();
            return AIQueryResult.Success(new JsonObject
            {
                ["studio_id"] = studioId.ToString(),
                ["studio_name"] = studio.StudioName,
                ["risk_groups"] = new JsonArray(riskGroups.ToArray()),
                ["total_risk_count"] = riskGroups.Count,
                ["generated_at"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetRiskGroupsTool error");
            return AIQueryResult.Error("Da xay ra loi khi xac dinh nhom nguy co");
        }
    }
}
