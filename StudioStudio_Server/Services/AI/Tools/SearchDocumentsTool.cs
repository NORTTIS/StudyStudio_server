using System.Diagnostics;
using System.Text.Json.Nodes;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Interfaces;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services.AI.Tools;

/// <summary>
/// Search documents in a group using vector similarity (Hybrid RAG)
/// Validates: group membership, query not empty
/// Returns: Top-K relevant document chunks with relevance scores
/// </summary>
public class SearchDocumentsTool(
    IVectorDatabaseService qdrantService,
    IEmbeddingService embeddingService,
    IGroupParticipantRepository participantRepository,
    IGroupAttachmentRepository attachmentRepository,
    ILogger<SearchDocumentsTool> logger) : IAITool
{
    public string Name => "search_documents";
    public string Description => "Tim kiem noi dung trong tai lieu cua nhom. "
        + "IMPORTANT: query la tham so BAT BUOC (bat buoc phai co). "
        + "Dien noi dung cau hoi hoac tu khoa tim kiem vao query. "
        + "group_id duoc tu dong cung cap boi he thong. Khong can truyen group_id.";
    public string? OutputFormatHint => "Document detail mode (uu tien khi dung search_documents): "
        + "Tra loi CHI TIET, cover DAY DU noi dung tu ket qua tool, khong tom tat qua ngan. "
        + "Khong bo qua muc nao da tra ve. "
        + "Neu mot truong khong co trong tool result thi ghi ro 'Khong co du lieu' thay vi bo trong. "
        + "Neu co nhieu muc, sap xep theo do lien quan giam dan va tach ro rang tung muc de nguoi dung de do doi. "
        + "Chi ket luan sau khi da trinh bay day du bang chung tu ket qua.";

    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["query"] = new JsonObject { ["type"] = "string", ["description"] = "Cau hoi/tu khoa tim kiem (bat buoc)" },
            ["top_k"] = new JsonObject { ["type"] = "number", ["description"] = "So ket qua toi da (default 3)" },
            ["document_id"] = new JsonObject { ["type"] = "string", ["description"] = "Tim kiem trong tai lieu cu the (optional)" }
        },
        ["required"] = new JsonArray { "query" }
    };

    private static string? Js(JsonNode? n) => n?.GetValue<string>();
    private static int Ji(JsonNode? n) => n == null ? 0 : n.GetValue<int>();
    private static Guid? Jg(JsonNode? n) =>
        string.IsNullOrWhiteSpace(Js(n)) ? null : Guid.TryParse(Js(n), out var g) ? g : null;

    public bool ValidateParameters(JsonObject p)
    {
        return !string.IsNullOrWhiteSpace(Js(p["query"]));
    }

    public async Task<AIQueryResult> ExecuteAsync(
        AIQueryContext context, JsonObject parameters, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (!context.GroupId.HasValue)
                return AIQueryResult.Error("Khong co group_id - chi hoat dong trong group context");

            var query = Js(parameters["query"])!;
            var groupId = context.GroupId.Value;
            var topK = Ji(parameters["top_k"]);
            var rawDocumentId = Js(parameters["document_id"]);
            var documentId = Jg(parameters["document_id"]);
            if (topK <= 0) topK = 3;

            if (!string.IsNullOrWhiteSpace(rawDocumentId) && !documentId.HasValue)
            {
                documentId = await ResolveLatestDocumentIdAsync(groupId, rawDocumentId);
                if (!documentId.HasValue)
                {
                    return AIQueryResult.Error("document_id khong hop le - khong resolve duoc filename sang attachment/document GUID moi nhat trong nhom");
                }

                logger.LogInformation(
                    "[TOOL-RESOLVE-DOC] search_documents | input={Input} resolvedDocumentId={DocumentId} groupId={GroupId}",
                    rawDocumentId,
                    documentId.Value,
                    groupId);
            }

            logger.LogInformation("[TOOL-START] search_documents | query={Query} groupId={GroupId} topK={TopK}",
                query, groupId, topK);

            // Permission check
            if (!await participantRepository.IsUserInGroupAsync(groupId, context.UserId))
                return AIQueryResult.Error("Ban khong co quyen truy cap nhom nay");

            // Embed query
            var queryVector = await embeddingService.GenerateEmbeddingAsync(query);

            // Search Qdrant
            var results = await qdrantService.SearchVectorsAsync(
                queryVector, topK, groupId, documentId, cancellationToken);

            logger.LogInformation("[TOOL-QDRANT] search_documents | resultsCount={Count} elapsedMs={Ms}",
                results.Count, sw.ElapsedMilliseconds);

            // Build response
            var docs = results.Select(r => new JsonObject
            {
                ["document_id"] = r.Payload.GetValueOrDefault("documentId")?.ToString() ?? "",
                ["file_name"] = r.Payload.GetValueOrDefault("fileName")?.ToString() ?? "",
                ["chunk_index"] = r.Payload.GetValueOrDefault("chunkIndex") is int ci ? ci : 0,
                ["content"] = r.Payload.GetValueOrDefault("content")?.ToString() ?? "",
                ["relevance_score"] = Math.Round(r.Score, 4)
            }).ToList();

            sw.Stop();
            bool isEnglish = context.Language.ToLower() == "en";

            if (docs.Count == 0)
            {
                logger.LogWarning("[TOOL-EMPTY] search_documents | query={Query} — No documents found (Qdrant returned 0 results)",
                    query);
            }
            else
            {
                logger.LogInformation("[TOOL-SUCCESS] search_documents | docsReturned={Count} elapsedMs={Ms}",
                    docs.Count, sw.ElapsedMilliseconds);
            }

            var result = AIQueryResult.Success(new JsonObject
            {
                ["query"] = query,
                ["documents"] = new JsonArray(docs.Cast<JsonNode?>().ToArray()),
                ["total_found"] = docs.Count,
                ["qdrant_reachable"] = true,
                ["summary"] = isEnglish
                    ? $"Found {docs.Count} relevant document chunks"
                    : $"Tim thay {docs.Count} doan noi dung lien quan"
            }, sw.ElapsedMilliseconds);
            
            // Log data size info for context tracking
            var resultJson = result.ToJson();
            
            logger.LogInformation(
                "[SEARCH-RESULT] query={Query} docsFound={Total} contextSize={CharCount} (full data included)",
                query, docs.Count, resultJson.Length);
            
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "[TOOL-ERROR] search_documents | query={Query} — Unexpected error", Js(parameters["query"]));
            return AIQueryResult.Error($"search_documents failed: {ex.Message}");
        }
    }

    private async Task<Guid?> ResolveLatestDocumentIdAsync(Guid groupId, string rawDocumentId)
    {
        var documentName = rawDocumentId.Trim();
        if (string.IsNullOrWhiteSpace(documentName))
        {
            return null;
        }

        var matchedAttachment = await attachmentRepository.FindLatestByGroupIdAndDocumentNameAsync(
            groupId,
            documentName,
            DateTime.UtcNow);

        return matchedAttachment?.GroupAttachmentId;
    }
}
