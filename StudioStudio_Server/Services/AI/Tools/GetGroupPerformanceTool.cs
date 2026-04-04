using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.AI.Tools.Interfaces;

namespace StudioStudio_Server.Services.AI.Tools;

[DebuggerStepThrough]
public class GetGroupPerformanceTool : IAITool
{
    private readonly IGroupParticipantRepository _participantRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IAnalyticsRepository _analyticsRepository;
    private readonly ILogger<GetGroupPerformanceTool> _logger;

    public string Name => "get_group_performance";
    public string Description => "Lay chi tiet hieu suat cua nhom. Khong can tham so (group_id tu dong lay tu context).";
    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject { },
        ["required"] = new JsonArray()
    };

    public GetGroupPerformanceTool(
        IGroupParticipantRepository participantRepository,
        ITaskRepository taskRepository,
        IAnalyticsRepository analyticsRepository,
        ILogger<GetGroupPerformanceTool> logger)
    {
        _participantRepository = participantRepository;
        _taskRepository = taskRepository;
        _analyticsRepository = analyticsRepository;
        _logger = logger;
    }

    private static string? Js(JsonNode? n) => n?.GetValue<string>();

    public bool ValidateParameters(JsonObject p) => true;

    public async Task<AIQueryResult> ExecuteAsync(AIQueryContext context, JsonObject parameters, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (!context.GroupId.HasValue)
                return AIQueryResult.Error("Khong co group_id trong context");

            var groupId = context.GroupId.Value;

            if (!await _participantRepository.IsUserInGroupAsync(groupId, context.UserId))
                return AIQueryResult.Error("Ban khong co quyen");

            var taskStats = await _taskRepository.GetGroupTaskStatisticsAsync(groupId);

            var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
            var startDate = endDate.AddDays(-30);
            var analytics = await _analyticsRepository.GetGroupAnalyticsRangeAsync(groupId, startDate, endDate);

            var completionTrend = analytics.Count > 1
                ? analytics.OrderBy(a => a.Date).Select(a => a.CompletedTasks).ToList()
                : new List<int>();

            var avgCompletionRate = taskStats.TotalTasks > 0
                ? Math.Round((double)taskStats.CompletedTasks / taskStats.TotalTasks * 100, 1)
                : 0.0;

            var overdueRisk = taskStats.OverdueTasks > 2;

            var performanceScore = taskStats.TotalTasks > 0
                ? Math.Min(100, Math.Max(0, (int)(avgCompletionRate - (taskStats.OverdueTasks * 5))))
                : 0;

            sw.Stop();
            return AIQueryResult.Success(new JsonObject
            {
                ["group_id"] = groupId.ToString(),
                ["task_stats"] = new JsonObject
                {
                    ["total_tasks"] = taskStats.TotalTasks,
                    ["completed_tasks"] = taskStats.CompletedTasks,
                    ["pending_tasks"] = taskStats.TotalTasks - taskStats.CompletedTasks,
                    ["overdue_tasks"] = taskStats.OverdueTasks,
                    ["completion_rate"] = avgCompletionRate,
                    ["priority_breakdown"] = new JsonObject
                    {
                        ["high"] = taskStats.HighPriorityTasks,
                        ["medium"] = taskStats.MediumPriorityTasks,
                        ["low"] = taskStats.LowPriorityTasks
                    },
                    ["severity_breakdown"] = new JsonObject
                    {
                        ["critical"] = taskStats.CriticalSeverityTasks,
                        ["major"] = taskStats.MajorSeverityTasks,
                        ["moderate"] = taskStats.ModerateSeverityTasks,
                        ["minor"] = taskStats.MinorSeverityTasks
                    }
                },
                ["performance_metrics"] = new JsonObject
                {
                    ["completion_trend"] = new JsonArray(completionTrend.Select(v => JsonValue.Create(v)).ToArray()),
                    ["avg_completion_rate"] = avgCompletionRate,
                    ["overdue_risk"] = overdueRisk,
                    ["performance_score"] = performanceScore
                },
                ["risk_indicators"] = new JsonArray(
                    overdueRisk
                        ? new JsonNode[] { JsonValue.Create("Co nhieu cong viec qua han")! }
                        : Array.Empty<JsonNode>()
                ),
                ["generated_at"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetGroupPerformanceTool error");
            return AIQueryResult.Error("Da xay ra loi khi lay hieu suat nhom");
        }
    }
}
