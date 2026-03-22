using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.AI.Tools.Interfaces;

namespace StudioStudio_Server.Services.AI.Tools;

public class GetDeadlinesTool : IAITool
{
    private readonly ITaskRepository _taskRepository;
    private readonly IGroupParticipantRepository _participantRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<GetDeadlinesTool> _logger;

    public string Name => "get_deadlines";
    public string Description => "Lay deadline";
    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["group_id"] = new JsonObject { ["type"] = "string" },
            ["days_ahead"] = new JsonObject { ["type"] = "number" },
            ["limit"] = new JsonObject { ["type"] = "number" }
        },
        ["required"] = new JsonArray { "group_id" }
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

    public bool ValidateParameters(JsonObject p) =>
        Guid.TryParse(Js(p["group_id"]), out _);

    public async Task<AIQueryResult> ExecuteAsync(AIQueryContext context, JsonObject parameters, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (!Guid.TryParse(Js(parameters["group_id"]), out var groupId))
                return AIQueryResult.Error("Invalid group_id");

            var daysAhead = Ji(parameters["days_ahead"]);
            if (daysAhead <= 0) daysAhead = 7;

            var limit = Ji(parameters["limit"]);
            if (limit <= 0) limit = 10;

            if (!await _participantRepository.IsUserInGroupAsync(groupId, context.UserId))
                return AIQueryResult.Error("Ban khong co quyen");

            var now = DateTime.UtcNow;
            var endDate = now.AddDays(daysAhead);

            var (tasks, _) = await _taskRepository.GetGroupTasksWithFiltersAsync(
                groupId, 1, 200, null, null, null, null, null, null, null, null, null, "dueDate", true);

            var deadlineTasks = tasks
                .Where(t => t.DueDate.HasValue && t.DueDate.Value >= now && t.DueDate.Value <= endDate)
                .OrderBy(t => t.DueDate)
                .Take(limit)
                .ToList();

            var ownerIds = deadlineTasks.Select(t => t.OwnerId).Distinct().ToList();
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
                    ["status"] = t.GroupStatus?.StatusName ?? "Unknown",
                    ["assignee_name"] = owner != null
                        ? owner.FirstName + " " + owner.LastName
                        : "Unassigned",
                    ["priority"] = t.Priority.ToString(),
                    ["is_overdue"] = t.DueDate.Value < now
                };
            }).ToList();

            sw.Stop();
            bool isEnglish = context.Language.ToLower() == "en";
            return AIQueryResult.Success(new JsonObject
            {
                ["deadlines"] = new JsonArray(deadlines.ToArray()),
                ["total"] = deadlines.Count,
                ["date_range"] = new JsonObject
                {
                    ["from"] = now.ToString("yyyy-MM-dd"),
                    ["to"] = endDate.ToString("yyyy-MM-dd")
                },
                ["summary"] = isEnglish
                    ? "Found " + deadlines.Count + " deadlines"
                    : "Co " + deadlines.Count + " deadline"
            }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetDeadlinesTool error");
            return AIQueryResult.Error("Da xay ra loi");
        }
    }
}
