using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.AI.Tools.Interfaces;
using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Services.AI.Tools;

/// <summary>
/// Tool để lấy thống kê cá nhân của user (từ AnalyticsRepository)
/// Scope: Personal AI (UserId only)
/// </summary>
public class GetPersonalStatsTool : IAITool
{
    private readonly ITaskRepository _taskRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IAnalyticsRepository _analyticsRepository;
    private readonly ILogger<GetPersonalStatsTool> _logger;

    public string Name => "get_personal_stats";
    public string Description => "Lay thong ke nang suất ca nhan: task hoan thanh, ti le hoan thanh, muc do hoat dong. Khong can tham so.";

    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject { },
        ["required"] = new JsonArray()
    };

    public GetPersonalStatsTool(
        ITaskRepository taskRepository,
        IGroupRepository groupRepository,
        IAnalyticsRepository analyticsRepository,
        ILogger<GetPersonalStatsTool> logger)
    {
        _taskRepository = taskRepository;
        _groupRepository = groupRepository;
        _analyticsRepository = analyticsRepository;
        _logger = logger;
    }

    public bool ValidateParameters(JsonObject parameters) => true;

    public async Task<AIQueryResult> ExecuteAsync(AIQueryContext context, JsonObject parameters, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var now = DateTime.UtcNow;
            var weekStart = DateOnly.FromDateTime(now.AddDays(-(int)now.DayOfWeek));

            // Get personal tasks
            var allTasks = await _taskRepository.GetPersonalTasksByOwnerAsync(context.UserId);

            // Dung Progress>=100 lam dinh nghia "hoan thanh" (thong nhat voi GroupAnalyticsJob)
            var completedTasks = allTasks.Where(t => t.Progress >= 100).ToList();
            var pendingTasks = allTasks.Where(t => t.Progress < 100).ToList();

            var overdueTasks = pendingTasks
                .Where(t => t.DueDate.HasValue && t.DueDate.Value < now)
                .ToList();

            var upcomingTasks = pendingTasks
                .Where(t => t.DueDate.HasValue && t.DueDate.Value >= now && t.DueDate.Value <= now.AddDays(7))
                .ToList();

            // Get group memberships for context
            var userGroups = await _groupRepository.GetUserGroupsAsync(context.UserId);

            // Calculate completion rate
            double completionRate = allTasks.Count > 0
                ? Math.Round((double)completedTasks.Count / allTasks.Count * 100, 1)
                : 0;

            // Weekly tasks completed
            var weeklyCompleted = completedTasks
                .Where(t => t.UpdatedAt >= now.AddDays(-7))
                .ToList();

            // Calculate productivity score from activity logs (7-day rolling window)
            double productivityScore = 0;
            var userGroupIds = userGroups.Select(g => g.GroupId).ToList();
            if (userGroupIds.Count > 0)
            {
                var activityScores = await _analyticsRepository.GetUserGroupActivityScoresAsync(userGroupIds, context.UserId, now.AddDays(-7), now);
                productivityScore = activityScores.Values.Sum();
            }

            sw.Stop();

            return AIQueryResult.Success(new JsonObject
            {
                ["total_tasks"] = allTasks.Count,
                ["completed_tasks"] = completedTasks.Count,
                ["pending_tasks"] = pendingTasks.Count,
                ["overdue_tasks"] = overdueTasks.Count,
                ["upcoming_tasks_7days"] = upcomingTasks.Count,
                ["weekly_completed"] = weeklyCompleted.Count,
                ["completion_rate_percent"] = completionRate,
                ["total_groups"] = userGroups.Count,
                ["productivity_score"] = productivityScore,
                ["period"] = new JsonObject
                {
                    ["week_start"] = weekStart.ToString("yyyy-MM-dd"),
                    ["week_end"] = weekStart.AddDays(6).ToString("yyyy-MM-dd"),
                    ["today"] = DateOnly.FromDateTime(now).ToString("yyyy-MM-dd")
                },
                ["summary"] = $"Ban co {allTasks.Count} cong viec, {completedTasks.Count} da hoan thanh ({completionRate}%), " +
                    $"{overdueTasks.Count} qua han, {upcomingTasks.Count} deadline trong 7 ngay toi."
            }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetPersonalStatsTool error for UserId={UserId}", context.UserId);
            return AIQueryResult.Error("Da xay ra loi khi lay thong ke ca nhan.");
        }
    }
}
