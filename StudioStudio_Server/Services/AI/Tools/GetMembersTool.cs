using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.AI.Tools.Interfaces;

namespace StudioStudio_Server.Services.AI.Tools;

public class GetMembersTool : IAITool
{
    private readonly IGroupParticipantRepository _participantRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<GetMembersTool> _logger;

    public string Name => "get_members";
    public string Description => "Lay danh sach thanh vien nhom. Khong can group_id (tu dong lay tu he thong). Optional: role (loc theo vai tro).";
    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["role"] = new JsonObject { ["type"] = "string", ["description"] = "Loc theo vai tro: Owner, Moderator, Member, Commenter, Viewer (optional)" }
        },
        ["required"] = new JsonArray()
    };

    public GetMembersTool(IGroupParticipantRepository participantRepository, IUserRepository userRepository, ILogger<GetMembersTool> logger)
    { _participantRepository = participantRepository; _userRepository = userRepository; _logger = logger; }

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
            if (!await _participantRepository.IsUserInGroupAsync(groupId, context.UserId))
                return AIQueryResult.Error("Ban khong co quyen");
            var participants = await _participantRepository.GetAllByGroupIdAsync(groupId);
            if (!string.IsNullOrEmpty(roleFilter)) participants = participants.Where(p => p.Role.ToString().ToLower() == roleFilter.ToLower()).ToList();
            var userIds = participants.Select(p => p.UserId).ToList();
            var users = await _userRepository.GetByIdsAsync(userIds);
            var userDict = users.ToDictionary(u => u.UserId);
            var memberList = participants.Select(p => { var user = userDict.GetValueOrDefault(p.UserId); return new JsonObject { ["user_id"] = p.UserId.ToString(), ["name"] = user != null ? user.FirstName + " " + user.LastName : "Unknown", ["email"] = user?.Email ?? "", ["role"] = p.Role.ToString(), ["joined_at"] = p.CreatedAt.ToString("yyyy-MM-dd"), ["is_online"] = false }; }).ToList();
            sw.Stop();
            return AIQueryResult.Success(new JsonObject { ["members"] = new JsonArray(memberList.ToArray()), ["total"] = memberList.Count }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex) { _logger.LogError(ex, "GetMembersTool error"); return AIQueryResult.Error("Da xay ra loi"); }
    }
}
