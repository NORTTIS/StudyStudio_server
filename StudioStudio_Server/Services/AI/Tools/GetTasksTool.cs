using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudioStudio_Server.Models.Entities;
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
    public string Description => "Lay danh sach cong viec cua nhom. Parameters: group_id (bat buoc), status (optional), limit (default 20)";

    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["group_id"] = new JsonObject { ["type"] = "string" },
            ["status"] = new JsonObject { ["type"] = "string" },
            ["limit"] = new JsonObject { ["type"] = "number" }
        },
        ["required"] = new JsonArray { "group_id" }
    };

    public GetTasksTool(ITaskRepository taskRepository, IGroupParticipantRepository participantRepository, ILogger<GetTasksTool> logger)
    {
        _taskRepository = taskRepository;
        _participantRepository = participantRepository;
        _logger = logger;
    }

    private static string? JsonGetString(JsonNode? node) => node?.GetValue<string>();
    private static int JsonGetInt(JsonNode? node) => node == null ? 0 : node.GetValue<int>();

    public bool ValidateParameters(JsonObject parameters) =>
        Guid.TryParse(JsonGetString(parameters["group_id"]), out _);

    public async Task<AIQueryResult> ExecuteAsync(AIQueryContext context, JsonObject parameters, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var groupIdStr = JsonGetString(parameters["group_id"]);
            if (!Guid.TryParse(groupIdStr, out var groupId))
                return AIQueryResult.Error("Invalid or missing group_id");

            var status = JsonGetString(parameters["status"]);
            var limit = JsonGetInt(parameters["limit"]);
            if (limit <= 0) limit = 20;

            var isMember = await _participantRepository.IsUserInGroupAsync(groupId, context.UserId);
            if (!isMember) return AIQueryResult.Error("Ban khong co quyen truy cap nhom nay");

            var tasks = await _taskRepository.GetAssignedGroupTasksByUserAsync(context.UserId);
            tasks = tasks.Where(t => t.GroupId == groupId).ToList();

            if (!string.IsNullOrEmpty(status))
                tasks = tasks.Where(t => t.GroupStatus?.StatusName?.ToLower() == status.ToLower()).ToList();

            var taskList = tasks.Take(limit).ToList();
            var formattedTasks = taskList.Select(t => new JsonObject
            {
                ["id"] = t.TaskId.ToString(),
                ["title"] = t.Title ?? "",
                ["status"] = t.GroupStatus?.StatusName ?? "Unknown",
                ["priority"] = t.Priority.ToString(),
                ["due_date"] = t.DueDate?.ToString("yyyy-MM-dd HH:mm") ?? "",
                ["assignee_name"] = t.Owner != null ? $"{t.Owner.FirstName} {t.Owner.LastName}".Trim() : "Unassigned",
                ["is_completed"] = t.GroupStatus?.StatusName?.ToLower() == "completed",
                ["is_overdue"] = t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow
            }).ToList();

            sw.Stop();
            return AIQueryResult.Success(new JsonObject
            {
                ["tasks"] = new JsonArray(formattedTasks.ToArray()),
                ["total"] = tasks.Count
            }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetTasksTool error");
            return AIQueryResult.Error("Da xay ra loi khi lay danh sach cong viec.");
        }
    }
}
