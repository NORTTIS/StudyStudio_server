using System.Diagnostics;
using System.Text.Json.Nodes;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Interfaces;
using StudioStudio_Server.Services.AI.Models;

namespace StudioStudio_Server.Services.AI.Tools;

public class GetGroupStatsTool(
    ITaskRepository taskRepository,
    IGroupParticipantRepository participantRepository,
    IGroupRepository groupRepository,
    ILogger<GetGroupStatsTool> logger) : IAITool
{
    public string Name => "get_group_stats";
    public string Description => "Lay thong ke cua nhom: tong so task, da hoan thanh, dang lam, chua lam, qua han. Khong can tham so.";
    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject { },
        ["required"] = new JsonArray()
    };

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

            if (!await participantRepository.IsUserInGroupAsync(groupId, context.UserId))
                return AIQueryResult.Error("Ban khong co quyen");

            var group = await groupRepository.GetByIdAsync(groupId);
            if (group == null)
                return AIQueryResult.Error("Khong tim thay nhom");

            var taskSummary = await taskRepository.GetGroupTaskStatisticsAsync(groupId);
            var members = await participantRepository.GetAllByGroupIdAsync(groupId);

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
                    ["nearest_deadline"] = taskSummary.NearestDeadline?.ToString("yyyy-MM-dd HH:mm"),
                    ["priority_breakdown"] = new JsonObject
                    {
                        ["high"] = taskSummary.HighPriorityTasks,
                        ["medium"] = taskSummary.MediumPriorityTasks,
                        ["low"] = taskSummary.LowPriorityTasks
                    },
                    ["severity_breakdown"] = new JsonObject
                    {
                        ["critical"] = taskSummary.CriticalSeverityTasks,
                        ["major"] = taskSummary.MajorSeverityTasks,
                        ["moderate"] = taskSummary.ModerateSeverityTasks,
                        ["minor"] = taskSummary.MinorSeverityTasks
                    }
                },
                ["generated_at"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetGroupStatsTool error");
            return AIQueryResult.Error("Da xay ra loi");
        }
    }
}
