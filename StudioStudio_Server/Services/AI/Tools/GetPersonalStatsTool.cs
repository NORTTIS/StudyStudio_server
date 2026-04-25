using System.Diagnostics;
using System.Text.Json.Nodes;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Services.AI.Interfaces;

namespace StudioStudio_Server.Services.AI.Tools;

/// <summary>
/// Tool để lấy thống kê cá nhân của user (từ AnalyticsRepository)
/// Scope: Personal AI (UserId only)
/// </summary>
public class GetPersonalStatsTool(
    ITaskRepository taskRepository,
    IGroupRepository groupRepository,
    ILogger<GetPersonalStatsTool> logger) : IAITool
{
    public string Name => "get_personal_stats";
    public string Description => "Lay thong ke nang suất ca nhan: task hoan thanh, ti le hoan thanh, muc do hoat dong. Khong can tham so.";

    public string? PlanningHint => "Dùng tool này khi user hỏi tổng quan năng suất cá nhân, số task đã hoàn thành, pending, overdue, completion rate, hoặc muốn xác nhận thống kê cá nhân.";
    public string? AnswerStyleHint => "Trả lời trực tiếp bằng số liệu. Nếu user đưa ra con số sai, sửa rõ ngay câu đầu, ví dụ 'Theo dữ liệu hiện tại, bạn đã hoàn thành 15 công việc, không phải 200.' Không mở đầu bằng các cụm mơ hồ như 'Đã rõ' hoặc 'Mình hiểu rồi'.";
    public string? OutputFormatHint => "Ưu tiên 1 câu kết luận ngắn + 1 câu bổ sung nếu cần. Nếu liệt kê thêm, chỉ dùng bullet ngắn cho completed_tasks, pending_tasks, overdue_tasks. Không dùng bảng cho thống kê cá nhân ngắn.";

    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject { },
        ["required"] = new JsonArray()
    };

    public bool ValidateParameters(JsonObject parameters) => true;

    public async Task<AIQueryResult> ExecuteAsync(AIQueryContext context, JsonObject parameters, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var now = DateTime.UtcNow;
            var weekStart = DateOnly.FromDateTime(now.AddDays(-(int)now.DayOfWeek));

            var personalTasks = await taskRepository.GetPersonalTasksByOwnerAsync(context.UserId);
            var assignedGroupTasks = await taskRepository.GetAssignedGroupTasksByUserAsync(context.UserId);

            var taskEntries = personalTasks
                .Select(t => new TaskStatEntry(t, "personal"))
                .Concat(assignedGroupTasks.Select(t => new TaskStatEntry(t, "group")))
                .GroupBy(x => x.Task.TaskId)
                .Select(g => g.First())
                .ToList();

            var allTasks = taskEntries.Select(x => x.Task).ToList();

            // Dung Progress>=100 lam dinh nghia "hoan thanh" (thong nhat voi GroupAnalyticsJob)
            var completedTasks = taskEntries.Where(x => x.Task.Progress >= 100).ToList();
            var pendingTasks = taskEntries.Where(x => x.Task.Progress < 100).ToList();

            var overdueTasks = pendingTasks
                .Where(x => x.Task.DueDate.HasValue && x.Task.DueDate.Value < now)
                .ToList();

            var upcomingTasks = pendingTasks
                .Where(x => x.Task.DueDate.HasValue && x.Task.DueDate.Value >= now && x.Task.DueDate.Value <= now.AddDays(7))
                .ToList();

            // Get group memberships for context
            var userGroups = await groupRepository.GetUserGroupsAsync(context.UserId);

            // Calculate completion rate
            double completionRate = taskEntries.Count > 0
                ? Math.Round((double)completedTasks.Count / taskEntries.Count * 100, 1)
                : 0;
            var personalCount = taskEntries.Count(x => x.Source == "personal");
            var groupCount = taskEntries.Count(x => x.Source == "group");

            // Weekly tasks completed
            var weeklyCompleted = completedTasks
                .Where(x => x.Task.UpdatedAt >= now.AddDays(-7))
                .ToList();

            JsonObject BuildSourceStats(string source)
            {
                var sourceTasks = taskEntries.Where(x => x.Source == source).ToList();
                var sourceCompleted = sourceTasks.Count(x => x.Task.Progress >= 100);
                var sourcePending = sourceTasks.Count - sourceCompleted;
                var sourceOverdue = sourceTasks.Count(x => x.Task.Progress < 100 && x.Task.DueDate.HasValue && x.Task.DueDate.Value < now);
                var sourceUpcoming = sourceTasks.Count(x => x.Task.Progress < 100 && x.Task.DueDate.HasValue && x.Task.DueDate.Value >= now && x.Task.DueDate.Value <= now.AddDays(7));
                var sourceWeeklyCompleted = sourceTasks.Count(x => x.Task.Progress >= 100 && x.Task.UpdatedAt >= now.AddDays(-7));
                var sourceCompletionRate = sourceTasks.Count > 0
                    ? Math.Round((double)sourceCompleted / sourceTasks.Count * 100, 1)
                    : 0;

                return new JsonObject
                {
                    ["source"] = source,
                    ["total_tasks"] = sourceTasks.Count,
                    ["completed_tasks"] = sourceCompleted,
                    ["pending_tasks"] = sourcePending,
                    ["overdue_tasks"] = sourceOverdue,
                    ["upcoming_tasks_7days"] = sourceUpcoming,
                    ["weekly_completed"] = sourceWeeklyCompleted,
                    ["completion_rate_percent"] = sourceCompletionRate,
                    ["priority_breakdown"] = new JsonObject
                    {
                        ["high"] = sourceTasks.Count(x => x.Task.Priority == TaskPriority.High),
                        ["medium"] = sourceTasks.Count(x => x.Task.Priority == TaskPriority.Medium),
                        ["low"] = sourceTasks.Count(x => x.Task.Priority == TaskPriority.Low)
                    },
                    ["severity_breakdown"] = new JsonObject
                    {
                        ["critical"] = sourceTasks.Count(x => x.Task.Severity == TaskSeverity.Critical),
                        ["major"] = sourceTasks.Count(x => x.Task.Severity == TaskSeverity.Major),
                        ["moderate"] = sourceTasks.Count(x => x.Task.Severity == TaskSeverity.Moderate),
                        ["minor"] = sourceTasks.Count(x => x.Task.Severity == TaskSeverity.Minor)
                    }
                };
            }


            sw.Stop();

            return AIQueryResult.Success(new JsonObject
            {
                ["total_tasks"] = taskEntries.Count,
                ["completed_tasks"] = completedTasks.Count,
                ["pending_tasks"] = pendingTasks.Count,
                ["overdue_tasks"] = overdueTasks.Count,
                ["upcoming_tasks_7days"] = upcomingTasks.Count,
                ["weekly_completed"] = weeklyCompleted.Count,
                ["completion_rate_percent"] = completionRate,
                ["total_groups"] = userGroups.Count,
                ["personal_count"] = personalCount,
                ["group_count"] = groupCount,
                ["period"] = new JsonObject
                {
                    ["week_start"] = weekStart.ToString("yyyy-MM-dd"),
                    ["week_end"] = weekStart.AddDays(6).ToString("yyyy-MM-dd"),
                    ["today"] = DateOnly.FromDateTime(now).ToString("yyyy-MM-dd")
                },
                ["source_breakdown"] = new JsonObject
                {
                    ["personal"] = BuildSourceStats("personal"),
                    ["group"] = BuildSourceStats("group")
                },
                ["priority_breakdown"] = new JsonObject
                {
                    ["high"] = allTasks.Count(t => t.Priority == TaskPriority.High),
                    ["medium"] = allTasks.Count(t => t.Priority == TaskPriority.Medium),
                    ["low"] = allTasks.Count(t => t.Priority == TaskPriority.Low)
                },
                ["severity_breakdown"] = new JsonObject
                {
                    ["critical"] = allTasks.Count(t => t.Severity == TaskSeverity.Critical),
                    ["major"] = allTasks.Count(t => t.Severity == TaskSeverity.Major),
                    ["moderate"] = allTasks.Count(t => t.Severity == TaskSeverity.Moderate),
                    ["minor"] = allTasks.Count(t => t.Severity == TaskSeverity.Minor)
                },
                ["summary"] = $"Ban co {taskEntries.Count} cong viec ({personalCount} personal, {groupCount} group), " +
                    $"{completedTasks.Count} da hoan thanh ({completionRate}%), {overdueTasks.Count} qua han, {upcomingTasks.Count} deadline trong 7 ngay toi."
            }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetPersonalStatsTool error for UserId={UserId}", context.UserId);
            return AIQueryResult.Error("Da xay ra loi khi lay thong ke ca nhan.");
        }
    }

    private sealed record TaskStatEntry(TaskItem Task, string Source);
}
