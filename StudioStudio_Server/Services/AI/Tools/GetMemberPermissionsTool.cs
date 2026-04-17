using System.Diagnostics;
using System.Text.Json.Nodes;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Interfaces;
using StudioStudio_Server.Services.AI.Models;

namespace StudioStudio_Server.Services.AI.Tools;

[DebuggerStepThrough]
public class GetMemberPermissionsTool(
    IStudioRepository studioRepository,
    IStudioParticipantRepository participantRepository,
    ILogger<GetMemberPermissionsTool> logger) : IAITool
{
    public string Name => "get_member_permissions";
    public string Description => "Kiem tra quyen cua thanh vien trong Studio. Khong can tham so (studio_id tu dong lay tu context).";
    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["user_id"] = new JsonObject { ["type"] = "string" }
        },
        ["required"] = new JsonArray()
    };

    private static string? Js(JsonNode? n) => n?.GetValue<string>();

    public bool ValidateParameters(JsonObject p) => true;

    public async Task<AIQueryResult> ExecuteAsync(AIQueryContext context, JsonObject parameters, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (!context.StudioId.HasValue)
                return AIQueryResult.Error("Khong co studio_id trong context");

            var studioId = context.StudioId.Value;

            var targetUserId = !string.IsNullOrEmpty(Js(parameters["user_id"]))
                ? Guid.Parse(Js(parameters["user_id"])! )
                : context.UserId;

            var studio = await studioRepository.GetByIdAsync(studioId);
            if (studio == null)
                return AIQueryResult.Error("Khong tim thay Studio");

            var isOwner = studio.OwnerId == targetUserId;
            var role = isOwner ? "owner" : "member";

            if (!isOwner)
            {
                var participant = await participantRepository.GetByStudioAndUserAsync(studioId, targetUserId);
                if (participant != null && participant.Role != null)
                {
                    role = participant.Role.ToString()?.ToLower() ?? "member";
                }
            }

            // Build permissions based on role
            var permissions = BuildPermissions(isOwner, role);

            sw.Stop();
            return AIQueryResult.Success(new JsonObject
            {
                ["user_id"] = targetUserId.ToString(),
                ["studio_id"] = studioId.ToString(),
                ["is_owner"] = isOwner,
                ["role"] = role,
                ["permissions"] = new JsonArray(permissions.Select(p => JsonValue.Create(p)).ToArray()),
                ["generated_at"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetMemberPermissionsTool error");
            return AIQueryResult.Error("Da xay ra loi khi kiem tra quyen thanh vien");
        }
    }

    private static List<string> BuildPermissions(bool isOwner, string role)
    {
        var permissions = new List<string>();

        // Base permissions for all members
        permissions.Add("view_studio");
        permissions.Add("view_groups");
        permissions.Add("view_tasks");
        permissions.Add("create_personal_tasks");
        permissions.Add("update_own_profile");

        if (isOwner)
        {
            // Owner permissions
            permissions.Add("manage_studio");
            permissions.Add("delete_studio");
            permissions.Add("manage_members");
            permissions.Add("manage_groups");
            permissions.Add("manage_all_tasks");
            permissions.Add("view_analytics");
            permissions.Add("manage_subscription");
            permissions.Add("invite_members");
            permissions.Add("remove_members");
            permissions.Add("transfer_ownership");
        }
        else
        {
            // Member permissions
            permissions.Add("create_group_tasks");
            permissions.Add("update_group_tasks");
            permissions.Add("view_own_analytics");
        }

        return permissions;
    }
}
