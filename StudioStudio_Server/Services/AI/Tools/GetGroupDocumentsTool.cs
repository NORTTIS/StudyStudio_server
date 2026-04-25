using System.Diagnostics;
using System.Text.Json.Nodes;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Interfaces;
using StudioStudio_Server.Services.AI.Models;

namespace StudioStudio_Server.Services.AI.Tools;

[DebuggerStepThrough]
public class GetGroupDocumentsTool(
    IGroupParticipantRepository participantRepository,
    IGroupRepository groupRepository,
    IGroupAttachmentRepository attachmentRepository,
    ILogger<GetGroupDocumentsTool> logger) : IAITool
{
    public string Name => "get_group_documents";
    public string Description => "Lay danh sach tai lieu da tai len cua nhom. Khong can tham so (group_id tu dong lay tu context).";
    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["limit"] = new JsonObject { ["type"] = "integer", ["default"] = 20 }
        },
        ["required"] = new JsonArray()
    };

    private static int Ji(JsonNode? n) => n?.GetValue<int>() ?? 0;

    public bool ValidateParameters(JsonObject p) => true;

    public async Task<AIQueryResult> ExecuteAsync(AIQueryContext context, JsonObject parameters, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (!context.GroupId.HasValue)
                return AIQueryResult.Error("Khong co group_id trong context");

            var groupId = context.GroupId.Value;

            if (!await participantRepository.IsUserInGroupAsync(groupId, context.UserId))
                return AIQueryResult.Error("Ban khong co quyen truy cap tai lieu nhom nay");

            var group = await groupRepository.GetByIdAsync(groupId);
            if (group == null)
                return AIQueryResult.Error("Khong tim thay nhom");

            var rawLimit = Ji(parameters["limit"]);
            var limit = rawLimit > 0 ? Math.Clamp(rawLimit, 1, 100) : 20;
            var totalCount = await attachmentRepository.CountByGroupIdAsync(groupId);
            var shownAttachments = await attachmentRepository.GetByGroupIdPagedAsync(groupId, 0, limit);

            var shownList = shownAttachments
                .Select(a => new JsonObject
                {
                    ["document_id"] = a.GroupAttachmentId.ToString(),
                    ["file_name"] = a.FileName,
                    ["content_type"] = a.FileType,
                    ["file_size"] = a.FileSize,
                    ["uploaded_by"] = a.UploadedBy.ToString(),
                    ["uploaded_at"] = a.UploadedAt.ToString("yyyy-MM-dd HH:mm:ss")
                })
                .ToList();

            sw.Stop();
            var documentsArray = new JsonArray();
            foreach (var doc in shownList)
            {
                documentsArray.Add(doc);
            }

            var result = AIQueryResult.Success(new JsonObject
            {
                ["group_id"] = groupId.ToString(),
                ["documents"] = documentsArray,
                ["total_count"] = totalCount,
                ["shown_count"] = shownList.Count,
                ["summary"] = totalCount > 0
                    ? $"Tim thay {totalCount} tai lieu trong nhom '{group.GroupName}'. Hien thi {shownList.Count} tai lieu."
                    : "Khong co tai lieu nao duoc tai len nhom nay."
            }, sw.ElapsedMilliseconds);

            var resultJson = result.ToJson();

            logger.LogInformation(
                "[DOCS-RESULT] totalCount={Total} shownCount={Shown} contextSize={CharCount} (full data included)",
                totalCount, shownList.Count, resultJson.Length);
            
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetGroupDocumentsTool error");
            return AIQueryResult.Error("Da xay ra loi khi lay danh sach tai lieu");
        }
    }
}
