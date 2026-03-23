using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.AI.Tools.Interfaces;

namespace StudioStudio_Server.Services.AI.Tools;

public class GetTasksTool : IAITool
{
    private readonly ITaskRepository _taskRepository;
    private readonly IGroupParticipantRepository _participantRepository;
    private readonly ILogger<GetTasksTool> _logger;

    public string Name => "get_tasks";
    public string Description => "Lay danh sach cong viec cua nhom. "
        + "Owner/Moderator: thay tat ca task. Member: chi task duoc assign. "
        + "Parameters: status (optional, loc theo trang thai), limit (optional, mac dinh 20). "
        + "group_id tu dong lay tu he thong.";

    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["status"] = new JsonObject { ["type"] = "string", ["description"] = "Loc theo trang thai (optional)" },
            ["limit"] = new JsonObject { ["type"] = "number", ["description"] = "So luong toi da (default 20)" }
        },
        ["required"] = new JsonArray()
    };

    public GetTasksTool(
        ITaskRepository taskRepository,
        IGroupParticipantRepository participantRepository,
        ILogger<GetTasksTool> logger)
    {
        _taskRepository = taskRepository;
        _participantRepository = participantRepository;
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
            if (!context.GroupId.HasValue)
                return AIQueryResult.Error("Khong co group_id - chi hoat dong trong group context");

            var groupId = context.GroupId.Value;

            if (!await _participantRepository.IsUserInGroupAsync(groupId, context.UserId))
                return AIQueryResult.Error("Ban khong co quyen truy cap nhom nay");

            // Role check: Owner/Moderator thay tat ca, Member chi task duoc assign
            var role = await _participantRepository.GetGroupRoleByUserIdAsync(context.UserId, groupId);
            if (role == GroupRole.Commenter || role == GroupRole.Viewer)
                return AIQueryResult.Error("Ban khong co quyen su dung AI nhom nay");

            var statusFilter = Js(parameters["status"]);
            var limit = Ji(parameters["limit"]);
            if (limit <= 0) limit = 20;

            // Owner/Moderator: lay tat ca task; Member: chi task duoc assign
            Guid? assigneeId = (role == GroupRole.Owner || role == GroupRole.Moderator) ? null : context.UserId;

            var (tasks, total) = await _taskRepository.GetGroupTasksWithFiltersAsync(
                groupId, 1, 100, null, assigneeId, null, null, null, null, null, null, null, "dueDate", true);

            // Filter by status if provided (partial match, case-insensitive)
            if (!string.IsNullOrEmpty(statusFilter))
                tasks = tasks.Where(t => t.GroupStatus?.StatusName?.ToLower().Contains(statusFilter.ToLower()) == true).ToList();

            var taskList = tasks.Take(limit).ToList();
            var formattedTasks = taskList.Select(t => new JsonObject
            {
                ["id"] = t.TaskId.ToString(),
                ["title"] = t.Title ?? "",
                ["status"] = t.GroupStatus?.StatusName ?? "Khong co trang thai",
                ["status_category"] = t.Progress >= 100 ? "Completed"
                    : t.Progress > 0 ? "InProgress"
                    : "NotStarted",
                ["priority"] = t.Priority.ToString(),
                ["progress"] = t.Progress,
                ["due_date"] = t.DueDate?.ToString("yyyy-MM-dd HH:mm") ?? "",
                ["assignee_name"] = t.Owner != null ? $"{t.Owner.FirstName} {t.Owner.LastName}".Trim() : "Unassigned",
                ["is_completed"] = t.Progress >= 100,
                ["is_overdue"] = t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow && t.Progress < 100,
                ["severity"] = t.Severity.ToString(),
                ["estimated_hours"] = t.EstimatedHours.HasValue ? JsonValue.Create(t.EstimatedHours.Value) : null,
                ["actual_hours"] = t.ActualHours.HasValue ? JsonValue.Create(t.ActualHours.Value) : null
            }).ToList();

            sw.Stop();
            return AIQueryResult.Success(new JsonObject
            {
                ["tasks"] = new JsonArray(formattedTasks.ToArray()),
                ["total"] = total,
                ["returned"] = formattedTasks.Count,
                ["scope"] = (role == GroupRole.Owner || role == GroupRole.Moderator) ? "all_tasks" : "assigned_only"
            }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetTasksTool error");
            return AIQueryResult.Error("Da xay ra loi khi lay danh sach cong viec.");
        }
    }
}
