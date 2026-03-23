using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.AI.Tools.Interfaces;

namespace StudioStudio_Server.Services.AI.Tools;

/// <summary>
/// Tool để lấy deadline của công việc cá nhân (không cần group_id)
/// Scope: Personal AI (UserId only)
/// </summary>
public class GetPersonalDeadlinesTool : IAITool
{
    private readonly ITaskRepository _taskRepository;
    private readonly ILogger<GetPersonalDeadlinesTool> _logger;

    public string Name => "get_personal_deadlines";
    public string Description => "Lay danh sach deadline cua cong viec ca nhan. Parameters: days_ahead (so ngay, default 7), limit (default 10). Khong can group_id.";

    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["days_ahead"] = new JsonObject { ["type"] = "number", ["description"] = "So ngay de xem deadline (default 7)" },
            ["limit"] = new JsonObject { ["type"] = "number", ["description"] = "So luong toi da (default 10)" }
        },
        ["required"] = new JsonArray()
    };

    public GetPersonalDeadlinesTool(ITaskRepository taskRepository, ILogger<GetPersonalDeadlinesTool> logger)
    {
        _taskRepository = taskRepository;
        _logger = logger;
    }

    private static string? Js(JsonNode? n) => n?.GetValue<string>();
    private static int Ji(JsonNode? n) => n == null ? 0 : n.GetValue<int>();

    public bool ValidateParameters(JsonObject parameters) => true;

    public async Task<AIQueryResult> ExecuteAsync(AIQueryContext context, JsonObject parameters, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var daysAhead = Ji(parameters["days_ahead"]);
            if (daysAhead <= 0) daysAhead = 7;

            var limit = Ji(parameters["limit"]);
            if (limit <= 0) limit = 10;

            var tasks = await _taskRepository.GetPersonalTasksByOwnerAsync(context.UserId);

            var now = DateTime.UtcNow;
            var endDate = now.AddDays(daysAhead);

            // Loc: co deadline, trong khoang, chua hoan thanh (Progress<100)
            var deadlineTasks = tasks
                .Where(t => t.DueDate.HasValue && t.DueDate.Value >= now && t.DueDate.Value <= endDate && t.Progress < 100)
                .OrderBy(t => t.DueDate)
                .Take(limit)
                .ToList();

            // Qua han: da qua han VA chua hoan thanh
            var overdueTasks = tasks
                .Where(t => t.DueDate.HasValue && t.DueDate.Value < now && t.Progress < 100)
                .OrderBy(t => t.DueDate)
                .Take(5)
                .ToList();

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
            _logger.LogError(ex, "GetPersonalDeadlinesTool error for UserId={UserId}", context.UserId);
            return AIQueryResult.Error("Da xay ra loi khi lay deadline ca nhan.");
        }
    }
}
