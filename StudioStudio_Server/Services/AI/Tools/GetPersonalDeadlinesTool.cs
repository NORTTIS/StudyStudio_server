using System.Diagnostics;
using System.Text.Json.Nodes;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Interfaces;
using StudioStudio_Server.Services.AI.Models;

namespace StudioStudio_Server.Services.AI.Tools;

/// <summary>
/// Tool để lấy deadline của công việc cá nhân (không cần group_id)
/// Scope: Personal AI (UserId only)
/// </summary>
public class GetPersonalDeadlinesTool(ITaskRepository taskRepository, ILogger<GetPersonalDeadlinesTool> logger) : IAITool
{
    private const int DefaultDaysAhead = 30;

    public string Name => "get_personal_deadlines";
    public string Description => $"Lay danh sach deadline cua cong viec ca nhan. Parameters: days_ahead (so ngay, default {DefaultDaysAhead}), limit (default 10). Khong can group_id.";

    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["days_ahead"] = new JsonObject { ["type"] = "number", ["description"] = $"So ngay de xem deadline (default {DefaultDaysAhead})" },
            ["limit"] = new JsonObject { ["type"] = "number", ["description"] = "So luong toi da (default 10)" }
        },
        ["required"] = new JsonArray()
    };

    private static string? Js(JsonNode? n) => n?.GetValue<string>();
    private static int Ji(JsonNode? n) => n == null ? 0 : n.GetValue<int>();

    public bool ValidateParameters(JsonObject parameters) => true;

    public async Task<AIQueryResult> ExecuteAsync(AIQueryContext context, JsonObject parameters, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var daysAhead = Ji(parameters["days_ahead"]);
            if (daysAhead <= 0) daysAhead = DefaultDaysAhead;

            var limit = Ji(parameters["limit"]);
            if (limit <= 0) limit = 10;

            var now = DateTime.UtcNow;
            var endDate = now.AddDays(daysAhead);

            // Upcoming deadlines: filter at DB layer to avoid loading all tasks
            var deadlineTasks = await taskRepository.GetPersonalTasksByOwnerWithDeadlineAsync(
                context.UserId, now, endDate, limit);

            // Overdue: bounded look-back to avoid loading all history
            // Note: Progress < 100 is already filtered in the repo query
            var overdueTasks = await taskRepository.GetPersonalTasksByOwnerWithDeadlineAsync(
                context.UserId, now.AddDays(-30), now, 5);

            var deadlines = deadlineTasks.Select(t => new JsonObject
            {
                ["task_id"] = t.TaskId.ToString(),
                ["title"] = t.Title ?? "",
                ["due_date"] = t.DueDate!.Value.ToString("yyyy-MM-dd HH:mm"),
                ["days_remaining"] = (int)(t.DueDate.Value - now).TotalDays,
                ["hours_remaining"] = (int)(t.DueDate.Value - now).TotalHours,
                ["status"] = t.PersonalStatus?.StatusName ?? "Unknown",
                ["priority"] = t.Priority.ToString()
            }).ToList();

            var overdue = overdueTasks.Select(t => new JsonObject
            {
                ["task_id"] = t.TaskId.ToString(),
                ["title"] = t.Title ?? "",
                ["due_date"] = t.DueDate!.Value.ToString("yyyy-MM-dd HH:mm"),
                ["days_overdue"] = (int)(now - t.DueDate.Value).TotalDays,
                ["status"] = t.PersonalStatus?.StatusName ?? "Unknown",
                ["priority"] = t.Priority.ToString()
            }).ToList();

            sw.Stop();
            bool isEnglish = context.Language?.ToLower() == "en";

            return AIQueryResult.Success(new JsonObject
            {
                ["upcoming_deadlines"] = new JsonArray(deadlines.ToArray()),
                ["overdue_tasks"] = new JsonArray(overdue.ToArray()),
                ["total_upcoming"] = deadlines.Count,
                ["total_overdue"] = overdue.Count,
                ["date_range"] = new JsonObject
                {
                    ["from"] = now.ToString("yyyy-MM-dd"),
                    ["to"] = endDate.ToString("yyyy-MM-dd")
                },
                ["summary"] = isEnglish
                    ? $"You have {deadlines.Count} upcoming deadlines in {daysAhead} days and {overdue.Count} overdue tasks."
                    : $"Ban co {deadlines.Count} deadline sap toi trong {daysAhead} ngay va {overdue.Count} cong viec qua han."
            }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetPersonalDeadlinesTool error for UserId={UserId}", context.UserId);
            return AIQueryResult.Error("Da xay ra loi khi lay deadline ca nhan.");
        }
    }
}
