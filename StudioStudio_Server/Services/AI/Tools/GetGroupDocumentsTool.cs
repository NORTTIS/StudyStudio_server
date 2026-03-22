using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.AI.Tools.Interfaces;

namespace StudioStudio_Server.Services.AI.Tools;

[DebuggerStepThrough]
public class GetGroupDocumentsTool : IAITool
{
    private readonly IGroupParticipantRepository _participantRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IGroupAttachmentRepository _attachmentRepository;
    private readonly ILogger<GetGroupDocumentsTool> _logger;

    public string Name => "get_group_documents";
    public string Description => "Lay danh sach tai lieu da tai len mot nhom. Parameters: group_id (required), limit (optional, default 20)";
    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["group_id"] = new JsonObject { ["type"] = "string" },
            ["limit"] = new JsonObject { ["type"] = "integer", ["default"] = 20 }
        },
        ["required"] = new JsonArray { "group_id" }
    };

    public GetGroupDocumentsTool(
        IGroupParticipantRepository participantRepository,
        IGroupRepository groupRepository,
        IGroupAttachmentRepository attachmentRepository,
        ILogger<GetGroupDocumentsTool> logger)
    {
        _participantRepository = participantRepository;
        _groupRepository = groupRepository;
        _attachmentRepository = attachmentRepository;
        _logger = logger;
    }

    private static string? Js(JsonNode? n) => n?.GetValue<string>();
    private static int Ji(JsonNode? n) => n?.GetValue<int>() ?? 0;

    public bool ValidateParameters(JsonObject p) =>
        Guid.TryParse(Js(p["group_id"]), out _);

    public async Task<AIQueryResult> ExecuteAsync(AIQueryContext context, JsonObject parameters, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (!Guid.TryParse(Js(parameters["group_id"]), out var groupId))
                return AIQueryResult.Error("Invalid group_id");

            if (!await _participantRepository.IsUserInGroupAsync(groupId, context.UserId))
                return AIQueryResult.Error("Ban khong co quyen truy cap tai lieu nhom nay");

            var group = await _groupRepository.GetByIdAsync(groupId);
            if (group == null)
                return AIQueryResult.Error("Khong tim thay nhom");

            var limit = Math.Clamp(Ji(parameters["limit"]), 1, 100);
            var allAttachments = await _attachmentRepository.GetByGroupIdAsync(groupId);
            var totalCount = allAttachments.Count;
            var shownAttachments = allAttachments
                .OrderByDescending(a => a.UploadedAt)
                .Take(limit)
                .Select(a => new JsonObject
                {
                    ["file_name"] = a.FileName,
                    ["content_type"] = a.FileType,
                    ["file_size"] = a.FileSize,
                    ["uploaded_by"] = a.UploadedBy.ToString(),
                    ["uploaded_at"] = a.UploadedAt.ToString("yyyy-MM-dd HH:mm:ss")
                })
                .ToList();

            sw.Stop();
            return AIQueryResult.Success(new JsonObject
            {
                ["group_id"] = groupId.ToString(),
                ["documents"] = new JsonArray(shownAttachments.ToArray()),
                ["total_count"] = totalCount,
                ["shown_count"] = shownAttachments.Count,
                ["summary"] = totalCount > 0
                    ? $"Tim thay {totalCount} tai lieu trong nhom '{group.GroupName}'. Hien thi {shownAttachments.Count} tai lieu."
                    : "Khong co tai lieu nao duoc tai len nhom nay."
            }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetGroupDocumentsTool error");
            return AIQueryResult.Error("Da xay ra loi khi lay danh sach tai lieu");
        }
    }
}
