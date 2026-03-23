using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.AI.Tools.Interfaces;

namespace StudioStudio_Server.Services.AI.Tools;

public class GetGroupStatsTool : IAITool
{
    private readonly ITaskRepository _taskRepository;
    private readonly IGroupParticipantRepository _participantRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly ILogger<GetGroupStatsTool> _logger;

    public string Name => "get_group_stats";
    public string Description => "Lay thong ke cua nhom: tong so task, da hoan thanh, dang lam, chua lam, qua han. Khong can tham so.";
    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject { },
        ["required"] = new JsonArray()
    };

    public GetGroupStatsTool(
        ITaskRepository taskRepository,
        IGroupParticipantRepository participantRepository,
        IGroupRepository groupRepository,
        ILogger<GetGroupStatsTool> logger)
    {
        _taskRepository = taskRepository;
        _participantRepository = participantRepository;
        _groupRepository = groupRepository;
        _logger = logger;
    }

    private static string? Js(JsonNode? n) => n?.GetValue<string>();

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

            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
                return AIQueryResult.Error("Khong tim thay nhom");

            var taskSummary = await _taskRepository.GetGroupTaskStatisticsAsync(groupId);
            var members = await _participantRepository.GetAllByGroupIdAsync(groupId);

            sw.Stop();
            return AIQueryResult.Success(new JsonObject
            {
                ["group_info"] = new JsonObject
                {
                    ["id"] = group.GroupId.ToString(),
                    ["name"] = group.GroupName ?? "",
                    ["member_count"] = members.Count,
                    ["created_at"] = group.CreatedAt.ToString("yyyy-MM-dd")
                },
                ["task_statistics"] = new JsonObject
                {
                    ["total_tasks"] = taskSummary.TotalTasks,
                    ["completed_tasks"] = taskSummary.CompletedTasks,
                    ["in_progress_tasks"] = taskSummary.InProgressTasks,
                    ["not_started_tasks"] = taskSummary.NotStartedTasks,
                    ["completion_percentage"] = taskSummary.CompletionPercentage,
                    ["pending_tasks"] = taskSummary.InProgressTasks + taskSummary.NotStartedTasks,
                    ["overdue_tasks"] = taskSummary.OverdueTasks,
                    ["nearest_deadline"] = taskSummary.NearestDeadline?.ToString("yyyy-MM-dd HH:mm")
                },
                ["generated_at"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetGroupStatsTool error");
            return AIQueryResult.Error("Da xay ra loi");
        }
    }
}
