using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.AI.Tools.Interfaces;

namespace StudioStudio_Server.Services.AI.Tools;

/// <summary>
/// Tool để lấy công việc của user (cá nhân + được assign từ active groups)
/// Scope: Personal AI (UserId only)
/// </summary>
public class GetPersonalTasksTool : IAITool
{
    private readonly ITaskRepository _taskRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly ILogger<GetPersonalTasksTool> _logger;

    public string Name => "get_personal_tasks";
    public string Description => "Lay danh sach tat ca cong viec cua nguoi dung (ca nhan va duoc assign tu nhom). Khong can group_id.";

    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["limit"] = new JsonObject { ["type"] = "number", ["description"] = "So luong toi da (default 20)" }
        },
        ["required"] = new JsonArray()
    };

    public GetPersonalTasksTool(
        ITaskRepository taskRepository,
        IGroupRepository groupRepository,
        ILogger<GetPersonalTasksTool> logger)
    {
        _taskRepository = taskRepository;
        _groupRepository = groupRepository;
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
            var limit = Ji(parameters["limit"]);
            if (limit <= 0) limit = 20;

            var now = DateTime.UtcNow;

            // 1. Personal tasks (OwnerId = userId, GroupId = null)
            var personalTasks = await _taskRepository.GetPersonalTasksByOwnerAsync(context.UserId);

            // 2. Assigned group tasks from active groups only
            var assignedTasks = await _taskRepository.GetAssignedGroupTasksByUserAsync(context.UserId);
            var userGroups = await _groupRepository.GetUserGroupsAsync(context.UserId);
            var activeGroupIds = userGroups.Where(g => g.IsActive).Select(g => g.GroupId).ToHashSet();

            var groupTasks = assignedTasks
                .Where(t => t.GroupId.HasValue && activeGroupIds.Contains(t.GroupId.Value))
                .ToList();

            // 3. Combine and mark source
            var allTasks = personalTasks.Select(t => new { Task = t, IsPersonal = true, GroupName = (string?)null })
                .Concat(groupTasks.Select(t => new { Task = t, IsPersonal = false, GroupName = t.Group?.GroupName }))
                .ToList();

            var totalPersonal = allTasks.Count(x => x.IsPersonal);
            var totalGroup = allTasks.Count(x => !x.IsPersonal);

            var result = allTasks.Take(limit).Select(x =>
            {
                var t = x.Task;
                // Dung Progress>=100 lam dinh nghia "hoan thanh" (thong nhat toan he thong)
                var isCompleted = t.Progress >= 100;

                return new JsonObject
                {
                    ["id"] = t.TaskId.ToString(),
                    ["title"] = t.Title ?? "",
                    ["status"] = x.IsPersonal ? t.PersonalStatus?.StatusName ?? "Unknown" : t.GroupStatus?.StatusName ?? "Unknown",
                    ["priority"] = t.Priority.ToString(),
                    ["progress"] = t.Progress,
                    ["due_date"] = t.DueDate?.ToString("yyyy-MM-dd HH:mm") ?? "",
                    ["is_completed"] = isCompleted,
                    ["is_overdue"] = t.DueDate.HasValue && t.DueDate.Value < now && !isCompleted,
                    ["source"] = x.IsPersonal ? "personal" : "group",
                    ["group_name"] = x.GroupName ?? "",
                    ["created_at"] = t.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                    ["severity"] = t.Severity.ToString(),
                    ["estimated_hours"] = t.EstimatedHours.HasValue ? JsonValue.Create(t.EstimatedHours.Value) : null,
                    ["actual_hours"] = t.ActualHours.HasValue ? JsonValue.Create(t.ActualHours.Value) : null
                };
            }).ToList();

            sw.Stop();
            return AIQueryResult.Success(new JsonObject
            {
                ["tasks"] = new JsonArray(result.ToArray()),
                ["total"] = allTasks.Count,
                ["personal_count"] = totalPersonal,
                ["group_count"] = totalGroup,
                ["summary"] = $"Ban co {totalPersonal} cong viec ca nhan va {totalGroup} cong viec tu nhom. Hien thi {result.Count} / {allTasks.Count}."
            }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetPersonalTasksTool error for UserId={UserId}", context.UserId);
            return AIQueryResult.Error("Da xay ra loi khi lay danh sach cong viec.");
        }
    }
}
