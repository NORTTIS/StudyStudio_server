using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Interfaces;
using StudioStudio_Server.Services.AI.Models;

namespace StudioStudio_Server.Services.AI.Tools;

/// <summary>
/// Tool phân tích rủi ro của một nhóm cụ thể
/// Dùng cho Group AI - chỉ phân tích nhóm hiện tại (từ context)
/// </summary>
[DebuggerStepThrough]
public class GetGroupRiskTool(
    IGroupRepository groupRepository,
    ITaskRepository taskRepository,
    IGroupParticipantRepository participantRepository,
    IAnalyticsRepository analyticsRepository,
    ILogger<GetGroupRiskTool> logger) : IAITool
{
    public string Name => "get_group_risk";
    public string Description => "Phan tich rui ro cua nhom hien tai. Khong can tham so - tu dong su dung GroupId tu context.";
    public string? PlanningHint => "Dung tool nay khi user hoi muc do rui ro cua nhom hien tai, ly do nhom dang an toan hay dang gap van de, hoac can giai thich bang so lieu vi sao nhom co rui ro.";
    public string? AnswerStyleHint => "Tra loi theo thu tu: so lieu truoc, danh gia sau. Neu ket luan nhom co rui ro, phai neu ro tung risk factor kem so lieu cu the, vi du ty le hoan thanh, so cong viec qua han, so thanh vien hoat dong. Khong mo dau bang cum mo ho nhu 'Da ro' hoac 'Minh hieu roi'.";
    public string? OutputFormatHint => "Trinh bay ngan gon theo 3 phan: (1) tom tat so lieu chinh cua nhom, (2) danh gia muc do rui ro, (3) risk factors va goi y tiep theo. Khong hien thi risk score dang so hoc nhu 'diem rui ro: 40'. Khi liet ke risk factors, moi factor phai kem so lieu cu the giai thich tai sao no la rui ro.";
    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject(),
        ["required"] = new JsonArray()
    };

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
            var group = await groupRepository.GetByIdAsync(groupId);
            if (group == null)
                return AIQueryResult.Error("Khong tim thay nhom");

            // Get task statistics
            var taskStats = await taskRepository.GetGroupTaskStatisticsAsync(groupId);

            // Get analytics for last 7 days
            var analytics = await analyticsRepository.GetGroupAnalyticsRangeAsync(groupId, sevenDaysAgo, DateOnly.FromDateTime(now));

            // Get members
            var members = await participantRepository.GetAllByGroupIdAsync(groupId);

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
            var recentCompletedTasks = recentActivity?.CompletedTasks ?? 0;

            // Risk factors
            var riskFactors = new List<string>();
            var riskFactorDetails = new JsonArray();
            if (completionRate < 50)
            {
                riskFactors.Add($"Ty le hoan thanh thap: {completedTasks}/{totalTasks} cong viec da hoan thanh ({completionRate}%), duoi nguong 50%");
                riskFactorDetails.Add(new JsonObject
                {
                    ["factor"] = "completion_rate",
                    ["severity"] = "high",
                    ["reason"] = "Ty le hoan thanh thap",
                    ["current_value"] = completionRate,
                    ["unit"] = "percent",
                    ["threshold"] = "< 50",
                    ["evidence"] = $"{completedTasks}/{totalTasks} cong viec da hoan thanh trong nhom"
                });
            }
            else if (completionRate < 70)
            {
                riskFactors.Add($"Ty le hoan thanh chua tot: {completedTasks}/{totalTasks} cong viec da hoan thanh ({completionRate}%), duoi muc ky vong 70%");
                riskFactorDetails.Add(new JsonObject
                {
                    ["factor"] = "completion_rate",
                    ["severity"] = "medium",
                    ["reason"] = "Ty le hoan thanh chua tot",
                    ["current_value"] = completionRate,
                    ["unit"] = "percent",
                    ["threshold"] = "< 70",
                    ["evidence"] = $"{completedTasks}/{totalTasks} cong viec da hoan thanh trong nhom"
                });
            }

            if (overdueTasks > 0)
            {
                riskFactors.Add($"Co {overdueTasks} cong viec qua han trong tong {pendingTasks} cong viec chua hoan thanh");
                riskFactorDetails.Add(new JsonObject
                {
                    ["factor"] = "overdue_tasks",
                    ["severity"] = overdueTasks >= 3 ? "high" : "medium",
                    ["reason"] = "Cong viec qua han",
                    ["current_value"] = overdueTasks,
                    ["unit"] = "tasks",
                    ["threshold"] = overdueTasks >= 3 ? ">= 3" : "> 0",
                    ["evidence"] = $"{overdueTasks} cong viec qua han, {pendingTasks} cong viec chua hoan thanh"
                });
            }

            if (!hasRecentActivity)
            {
                riskFactors.Add($"Khong co hoat dong dang ke trong 7 ngay gan day: {recentActiveMembers} thanh vien active, {recentMessages} tin nhan, {recentCompletedTasks} cong viec hoan thanh");
                riskFactorDetails.Add(new JsonObject
                {
                    ["factor"] = "recent_activity",
                    ["severity"] = "high",
                    ["reason"] = "Khong co hoat dong gan day",
                    ["current_value"] = 0,
                    ["unit"] = "activity",
                    ["threshold"] = "> 0",
                    ["evidence"] = $"{recentActiveMembers} thanh vien active, {recentMessages} tin nhan, {recentCompletedTasks} cong viec hoan thanh trong 7 ngay"
                });
            }

            if (pendingTasks > 10)
            {
                riskFactors.Add($"Ton dong nhieu cong viec: {pendingTasks} cong viec chua hoan thanh");
                riskFactorDetails.Add(new JsonObject
                {
                    ["factor"] = "pending_tasks",
                    ["severity"] = "medium",
                    ["reason"] = "Luong cong viec ton dong cao",
                    ["current_value"] = pendingTasks,
                    ["unit"] = "tasks",
                    ["threshold"] = "> 10",
                    ["evidence"] = $"{pendingTasks} cong viec dang cho, chi {completedTasks} cong viec da hoan thanh"
                });
            }

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
                    ["recent_completed_tasks"] = recentCompletedTasks,
                    ["has_recent_activity"] = hasRecentActivity
                },
                ["risk_level"] = riskLevel,
                ["risk_factors"] = new JsonArray(riskFactors.Select(f => JsonValue.Create(f)).ToArray()),
                ["risk_factor_details"] = riskFactorDetails,
                ["recommendations"] = new JsonArray(recommendations.Select(r => JsonValue.Create(r)).ToArray()),
                ["generated_at"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetGroupRiskTool error");
            return AIQueryResult.Error("Da xay ra loi khi phan tich rui ro nhom");
        }
    }
}
