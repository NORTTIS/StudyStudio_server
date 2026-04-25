using System.Diagnostics;
using System.Text.Json.Nodes;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Interfaces;
using StudioStudio_Server.Services.AI.Models;

namespace StudioStudio_Server.Services.AI.Tools;

public class GetMembersTool(IGroupParticipantRepository participantRepository, IUserRepository userRepository, ILogger<GetMembersTool> logger) : IAITool
{
    public string Name => "get_members";
    public string Description => "Lay danh sach thanh vien cua nhom hien tai trong Group AI context. Khong can group_id (tu dong lay tu he thong). Optional: role (loc theo vai tro). Neu user chi dinh mot nhom khac nhu 'group 2' thi khong duoc map sang nhom hien tai.";
    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["role"] = new JsonObject { ["type"] = "string", ["description"] = "Loc theo vai tro: Owner, Moderator, Member, Commenter, Viewer (optional)" },
            ["requested_group_reference"] = new JsonObject { ["type"] = "string", ["description"] = "System-inferred explicit group reference from the user's question. The LLM should not set this manually." }
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
            if (!context.GroupId.HasValue)
                return AIQueryResult.Error("Khong co group_id - chi hoat dong trong group context");

            var groupId = context.GroupId.Value;
            var roleFilter = Js(parameters["role"]);
            var requestedGroupReference = Js(parameters["requested_group_reference"]);

            if (!string.IsNullOrWhiteSpace(requestedGroupReference))
            {
                return AIQueryResult.Error($"Ban dang chi dinh group '{requestedGroupReference}', nhung Group AI chi doc du lieu cua nhom hien tai trong context. Hay chuyen sang dung nhom do hoac dung Master AI neu ban muon xem nhom khac.");
            }

            if (!await participantRepository.IsUserInGroupAsync(groupId, context.UserId))
                return AIQueryResult.Error("Ban khong co quyen");
            var participants = await participantRepository.GetAllByGroupIdAsync(groupId);
            if (!string.IsNullOrEmpty(roleFilter)) participants = participants.Where(p => p.Role.ToString().ToLower() == roleFilter.ToLower()).ToList();
            var userIds = participants.Select(p => p.UserId).ToList();
            var users = await userRepository.GetByIdsAsync(userIds);
            var userDict = users.ToDictionary(u => u.UserId);
            var memberList = participants.Select(p => { var user = userDict.GetValueOrDefault(p.UserId); return new JsonObject { ["user_id"] = p.UserId.ToString(), ["name"] = user != null ? user.FirstName + " " + user.LastName : "Unknown", ["email"] = user?.Email ?? "", ["role"] = p.Role.ToString(), ["joined_at"] = p.CreatedAt.ToString("yyyy-MM-dd"), ["is_online"] = false }; }).ToList();
            sw.Stop();
            return AIQueryResult.Success(new JsonObject { ["members"] = new JsonArray(memberList.ToArray()), ["total"] = memberList.Count }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex) { logger.LogError(ex, "GetMembersTool error"); return AIQueryResult.Error("Da xay ra loi"); }
    }
}
