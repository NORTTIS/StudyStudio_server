using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.AI.Tools.Interfaces;

namespace StudioStudio_Server.Services.AI.Tools;

/// <summary>
/// Tool phân tích rủi ro của một nhóm cụ thể
/// Dùng cho Group AI - chỉ phân tích nhóm hiện tại (từ context)
/// </summary>
[DebuggerStepThrough]
public class GetGroupRiskTool : IAITool
{
    private readonly IGroupRepository _groupRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IGroupParticipantRepository _participantRepository;
    private readonly IAnalyticsRepository _analyticsRepository;
    private readonly ILogger<GetGroupRiskTool> _logger;

    public string Name => "get_group_risk";
    public string Description => "Phan tich rui ro cua nhom hien tai. Khong can tham so - tu dong su dung GroupId tu context.";
    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject(),
        ["required"] = new JsonArray()
    };

    public GetGroupRiskTool(
        IGroupRepository groupRepository,
        ITaskRepository taskRepository,
        IGroupParticipantRepository participantRepository,
        IAnalyticsRepository analyticsRepository,
        ILogger<GetGroupRiskTool> logger)
    {
        _groupRepository = groupRepository;
        _taskRepository = taskRepository;
        _participantRepository = participantRepository;
        _analyticsRepository = analyticsRepository;
        _logger = logger;
    }

    public bool ValidateParameters(JsonObject p) => true; // No parameters needed

    public async Task<AIQueryResult> ExecuteAsync(AIQueryContext context, JsonObject parameters, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (!context.GroupId.HasValue)
                return AIQueryResult.Error("Khong co GroupId trong context");

            var groupId = context.GroupId.Value;
            var now = DateTime.UtcNow;
            var sevenDaysAgo = DateOnly.FromDateTime(now.AddDays(-7));

            // Get group info
            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
                return AIQueryResult.Error("Khong tim thay nhom");

            // Get task statistics
            var taskStats = await _taskRepository.GetGroupTaskStatisticsAsync(groupId);

            // Get analytics for last 7 days
            var analytics = await _analyticsRepository.GetGroupAnalyticsRangeAsync(groupId, sevenDaysAgo, DateOnly.FromDateTime(now));

            // Get members
            var members = await _participantRepository.GetAllByGroupIdAsync(groupId);

            // Calculate metrics
            var totalTasks = taskStats.TotalTasks;
            var completedTasks = taskStats.CompletedTasks;
            var overdueTasks = taskStats.OverdueTasks;
            var pendingTasks = totalTasks - completedTasks;
            var completionRate = totalTasks > 0 ? Math.Round((double)completedTasks / totalTasks * 100, 1) : 0.0;

            // Activity in last 7 days from analytics
            var recentActivity = analytics.FirstOrDefault();
            var hasRecentActivity = recentActivity != null && (recentActivity.ActiveMembers > 0 || recentActivity.MessagesCount > 0 || recentActivity.CompletedTasks > 0);
            var recentMessages = recentActivity?.MessagesCount ?? 0;
            var recentActiveMembers = recentActivity?.ActiveMembers ?? 0;

            // Risk factors
            var riskFactors = new List<string>();
            if (completionRate < 50) riskFactors.Add("Ty le hoan thanh thap");
            else if (completionRate < 70) riskFactors.Add("Ty le hoan thanh chua tot");
            if (overdueTasks > 0) riskFactors.Add($"Co {overdueTasks} cong viec qua han");
            if (!hasRecentActivity) riskFactors.Add("Khong co hoat dong gan day");

            // Risk level
            var riskScore = 0;
            if (completionRate < 50) riskScore += 30;
            else if (completionRate < 70) riskScore += 15;
            if (overdueTasks >= 3) riskScore += 25;
            else if (overdueTasks > 0) riskScore += 15;
            if (!hasRecentActivity) riskScore += 20;
            if (pendingTasks > 10) riskScore += 10;

            var riskLevel = riskScore >= 50 ? "HIGH" : riskScore >= 25 ? "MEDIUM" : "LOW";

            // Recommendations
            var recommendations = new List<string>();
            if (completionRate < 70) recommendations.Add("Tang toc do hoan thanh cong viec");
            if (overdueTasks > 0) recommendations.Add("Uu tien xu ly cac cong viec qua han");
            if (!hasRecentActivity) recommendations.Add("Kich hoat thanh vien bang cach tao cong viec moi hoac cuoc hop");
            if (pendingTasks > 10) recommendations.Add($"Co {pendingTasks} cong viec dang cho - can phan bo lai");

            sw.Stop();
            return AIQueryResult.Success(new JsonObject
            {
                ["group_id"] = groupId.ToString(),
                ["group_name"] = group.GroupName,
                ["member_count"] = members.Count,
                ["metrics"] = new JsonObject
                {
                    ["total_tasks"] = totalTasks,
                    ["completed_tasks"] = completedTasks,
                    ["pending_tasks"] = pendingTasks,
                    ["overdue_tasks"] = overdueTasks,
                    ["completion_rate"] = completionRate,
                    ["recent_messages"] = recentMessages,
                    ["recent_active_members"] = recentActiveMembers,
                    ["has_recent_activity"] = hasRecentActivity
                },
                ["risk_level"] = riskLevel,
                ["risk_score"] = riskScore,
                ["risk_factors"] = new JsonArray(riskFactors.Select(f => JsonValue.Create(f)).ToArray()),
                ["recommendations"] = new JsonArray(recommendations.Select(r => JsonValue.Create(r)).ToArray()),
                ["generated_at"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetGroupRiskTool error");
            return AIQueryResult.Error("Da xay ra loi khi phan tich rui ro nhom");
        }
    }
}
