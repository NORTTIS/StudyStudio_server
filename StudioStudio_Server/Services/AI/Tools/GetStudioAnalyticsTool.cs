using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.AI.Tools.Interfaces;

namespace StudioStudio_Server.Services.AI.Tools;

[DebuggerStepThrough]
public class GetStudioAnalyticsTool : IAITool
{
    private readonly IStudioRepository _studioRepository;
    private readonly IAnalyticsRepository _analyticsRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IGroupParticipantRepository _participantRepository;
    private readonly ILogger<GetStudioAnalyticsTool> _logger;

    public string Name => "get_studio_analytics";
    public string Description => "Lay thong ke tong the cua Studio. Parameters: studio_id (required), period (optional: week/month/all, default all)";
    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["studio_id"] = new JsonObject { ["type"] = "string" },
            ["period"] = new JsonObject { ["type"] = "string" }
        },
        ["required"] = new JsonArray { "studio_id" }
    };

    public GetStudioAnalyticsTool(
        IStudioRepository studioRepository,
        IAnalyticsRepository analyticsRepository,
        ITaskRepository taskRepository,
        IGroupParticipantRepository participantRepository,
        ILogger<GetStudioAnalyticsTool> logger)
    {
        _studioRepository = studioRepository;
        _analyticsRepository = analyticsRepository;
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

            var period = Js(parameters["period"]) ?? "all";
            var validPeriods = new[] { "week", "month", "all" };
            if (!validPeriods.Contains(period))
                return AIQueryResult.Error("Period phai la mot trong: week, month, all");

            var studio = await _studioRepository.GetByIdAsync(studioId);
            if (studio == null)
                return AIQueryResult.Error("Khong tim thay Studio");

            var groups = await _studioRepository.GetGroupsByStudioIdAsync(studioId);
            var totalGroups = groups.Count;

            int totalMembers = 0;
            int totalTasks = 0;
            int completedTasks = 0;
            int overdueTasks = 0;

            foreach (var g in groups)
            {
                var members = await _participantRepository.GetAllByGroupIdAsync(g.GroupId);
                totalMembers += members.Count;

                var taskStats = await _taskRepository.GetGroupTaskStatisticsAsync(g.GroupId);
                totalTasks += taskStats.TotalTasks;
                completedTasks += taskStats.CompletedTasks;
                overdueTasks += taskStats.OverdueTasks;
            }

            var completionRate = totalTasks > 0
                ? Math.Round((double)completedTasks / totalTasks * 100, 2)
                : 0.0;

            var result = new JsonObject
            {
                ["studio_id"] = studioId.ToString(),
                ["studio_name"] = studio.StudioName,
                ["period"] = period,
                ["summary"] = new JsonObject
                {
                    ["total_groups"] = totalGroups,
                    ["total_members"] = totalMembers,
                    ["total_tasks"] = totalTasks,
                    ["completed_tasks"] = completedTasks,
                    ["overdue_tasks"] = overdueTasks,
                    ["completion_rate"] = completionRate,
                    ["pending_tasks"] = totalTasks - completedTasks
                },
                ["generated_at"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            };

            // Get recent analytics for week/month period
            if (period != "all" && groups.Count > 0)
            {
                DateTime fromDate;
                if (period == "week")
                    fromDate = DateTime.UtcNow.AddDays(-7);
                else
                    fromDate = DateTime.UtcNow.AddDays(-30);

                var allGroupIds = groups.Select(g => g.GroupId).ToList();

                var createdByGroup = await _analyticsRepository.AggregateTasksCreatedByUserAsync(fromDate, DateTime.UtcNow);
                var completedByGroup = await _analyticsRepository.AggregateTasksCompletedByUserAsync(fromDate, DateTime.UtcNow);
                var messagesByGroup = await _analyticsRepository.AggregateMessagesByGroupAsync(groups.First().GroupId, fromDate, DateTime.UtcNow);

                result["recent_activity"] = new JsonObject
                {
                    ["from_date"] = fromDate.ToString("yyyy-MM-dd"),
                    ["to_date"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                    ["tasks_created"] = createdByGroup.Values.Sum(),
                    ["tasks_completed"] = completedByGroup.Values.Sum()
                };
            }

            sw.Stop();
            return AIQueryResult.Success(result, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetStudioAnalyticsTool error");
            return AIQueryResult.Error("Da xay ra loi khi lay thong ke Studio");
        }
    }
}
