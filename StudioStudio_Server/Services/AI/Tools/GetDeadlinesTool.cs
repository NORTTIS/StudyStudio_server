using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.AI.Tools.Interfaces;

namespace StudioStudio_Server.Services.AI.Tools;

public class GetDeadlinesTool : IAITool
{
    private const int DefaultDaysAhead = 30;
    private const int DefaultLimit = 10;

    private readonly ITaskRepository _taskRepository;
    private readonly IGroupParticipantRepository _participantRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<GetDeadlinesTool> _logger;

    public string Name => "get_deadlines";
    public string Description => "Lay danh sach deadline sap toi cua nhom. "
        + "Owner/Moderator: thay tat ca deadline. Member: chi deadline cua task duoc assign. "
        + $"Parameters: days_ahead (so ngay, default {DefaultDaysAhead}), limit (default {DefaultLimit}). "
        + "group_id tu dong lay tu he thong. Da hoan thanh (Progress=100) khong hien thi trong danh sach.";

    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["days_ahead"] = new JsonObject { ["type"] = "number", ["description"] = $"So ngay (default {DefaultDaysAhead})" },
            ["limit"] = new JsonObject { ["type"] = "number", ["description"] = $"So luong toi da (default {DefaultLimit})" }
        },
        ["required"] = new JsonArray()
    };

    public GetDeadlinesTool(
        ITaskRepository taskRepository,
        IGroupParticipantRepository participantRepository,
        IUserRepository userRepository,
        ILogger<GetDeadlinesTool> logger)
    {
        _taskRepository = taskRepository;
        _participantRepository = participantRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    private static string? Js(JsonNode? n) => n?.GetValue<string>();
    private static int Ji(JsonNode? n) => n == null ? 0 : n.GetValue<int>();

    public bool ValidateParameters(JsonObject p) => true;

    public async Task<AIQueryResult> ExecuteAsync(AIQueryContext context, JsonObject parameters, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (!context.GroupId.HasValue)
                return AIQueryResult.Error("Khong co group_id - chi hoat dong trong group context");

            var groupId = context.GroupId.Value;

            if (!await _participantRepository.IsUserInGroupAsync(groupId, context.UserId))
                return AIQueryResult.Error("Ban khong co quyen");

            // Role check
            var role = await _participantRepository.GetGroupRoleByUserIdAsync(context.UserId, groupId);
            if (role == GroupRole.Commenter || role == GroupRole.Viewer)
                return AIQueryResult.Error("Ban khong co quyen su dung AI nhom nay");

            var daysAhead = Ji(parameters["days_ahead"]);
            if (daysAhead <= 0) daysAhead = DefaultDaysAhead;

            var limit = Ji(parameters["limit"]);
            if (limit <= 0) limit = DefaultLimit;

            var now = DateTime.UtcNow;
            var endDate = now.AddDays(daysAhead);

            // Owner/Moderator: lay tat ca task; Member: chi task duoc assign
            Guid? assigneeId = (role == GroupRole.Owner || role == GroupRole.Moderator) ? null : context.UserId;

            var (tasks, _) = await _taskRepository.GetGroupTasksWithFiltersAsync(
                groupId, 1, 200, null, assigneeId, null, null, null, null, null,
                now,                   // dueDateFrom
                endDate,               // dueDateTo
                "dueDate", true,      // sortBy, sortAscending
                null, null,           // statusKeyword, statusCategory
                null, null);          // minPriority, minSeverity

            // Filter: co deadline, trong khoang thoi gian, chua hoan thanh
            var deadlineTasks = tasks
                .Where(t => t.DueDate.HasValue
                         && t.DueDate.Value >= now
                         && t.DueDate.Value <= endDate
                         && t.Progress < 100)
                .OrderBy(t => t.DueDate)
                .Take(limit)
                .ToList();

            // Overdue: load separately with a bounded look-back window
            var (allOverdue, _) = await _taskRepository.GetGroupTasksWithFiltersAsync(
                groupId, 1, 50, null, assigneeId, null, null, null, null, null,
                now.AddDays(-30),     // look back 30 days max
                null,                  // no upper bound
                "dueDate", true,
                null, null,
                null, null);

            var overdueTasks = allOverdue
                .Where(t => t.DueDate.HasValue
                         && t.DueDate.Value < now
                         && t.Progress < 100)
                .OrderBy(t => t.DueDate)
                .Take(5)
                .ToList();

            var ownerIds = deadlineTasks.Select(t => t.OwnerId)
                .Concat(overdueTasks.Select(t => t.OwnerId))
                .Distinct()
                .ToList();
            var owners = await _userRepository.GetByIdsAsync(ownerIds);
            var ownerDict = owners.ToDictionary(o => o.UserId);

            var deadlines = deadlineTasks.Select(t =>
            {
                var owner = ownerDict.GetValueOrDefault(t.OwnerId);
                return new JsonObject
                {
                    ["task_id"] = t.TaskId.ToString(),
                    ["title"] = t.Title ?? "",
                    ["due_date"] = t.DueDate.Value.ToString("yyyy-MM-dd HH:mm"),
                    ["days_remaining"] = (int)(t.DueDate.Value - now).TotalDays,
                    ["hours_remaining"] = (int)(t.DueDate.Value - now).TotalHours,
                    ["status"] = t.GroupStatus?.StatusName ?? "Khong co trang thai",
                    ["progress"] = t.Progress,
                    ["assignee_name"] = owner != null
                        ? owner.FirstName + " " + owner.LastName
                        : "Unassigned",
                    ["priority"] = t.Priority.ToString(),
                    // Qua han: DueDate < now VA chua hoan thanh
                    ["is_overdue"] = t.DueDate.Value < now && t.Progress < 100
                };
            }).ToList();

            var overdue = overdueTasks.Select(t =>
            {
                var owner = ownerDict.GetValueOrDefault(t.OwnerId);
                return new JsonObject
                {
                    ["task_id"] = t.TaskId.ToString(),
                    ["title"] = t.Title ?? "",
                    ["due_date"] = t.DueDate!.Value.ToString("yyyy-MM-dd HH:mm"),
                    ["days_overdue"] = (int)(now - t.DueDate.Value).TotalDays,
                    ["status"] = t.GroupStatus?.StatusName ?? "Khong co trang thai",
                    ["progress"] = t.Progress,
                    ["assignee_name"] = owner != null
                        ? owner.FirstName + " " + owner.LastName
                        : "Unassigned",
                    ["priority"] = t.Priority.ToString()
                };
            }).ToList();

            sw.Stop();
            bool isEnglish = context.Language.ToLower() == "en";
            return AIQueryResult.Success(new JsonObject
            {
                ["deadlines"] = new JsonArray(deadlines.ToArray()),
                ["overdue_tasks"] = new JsonArray(overdue.ToArray()),
                ["total"] = deadlines.Count,
                ["total_overdue"] = overdue.Count,
                ["date_range"] = new JsonObject
                {
                    ["from"] = now.ToString("yyyy-MM-dd"),
                    ["to"] = endDate.ToString("yyyy-MM-dd")
                },
                ["scope"] = (role == GroupRole.Owner || role == GroupRole.Moderator) ? "all_tasks" : "assigned_only",
                ["summary"] = isEnglish
                    ? "Found " + deadlines.Count + " upcoming deadlines and " + overdue.Count + " overdue tasks"
                    : "Co " + deadlines.Count + " deadline sap toi va " + overdue.Count + " cong viec qua han"
            }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetDeadlinesTool error");
            return AIQueryResult.Error("Da xay ra loi");
        }
    }
}
