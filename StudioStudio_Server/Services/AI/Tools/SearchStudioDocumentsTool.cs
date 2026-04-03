using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.AI.Tools.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services.AI.Tools;

/// <summary>
/// Search documents across all groups in a studio (studio-level Hybrid RAG)
/// Validates: query not empty, studio exists
/// Returns: Top-K relevant document chunks from all studio groups with group info
/// </summary>
public class SearchStudioDocumentsTool : IAITool
{
    private readonly IVectorDatabaseService _qdrantService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IStudioRepository _studioRepository;
    private readonly ILogger<SearchStudioDocumentsTool> _logger;

    public string Name => "search_studio_documents";
    public string Description => "Tim kiem tai lieu tren toan bo studio (tat ca cac nhom). "
        + "Parameters: query (bat buoc), studio_id (bat buoc), top_k (optional, mac dinh 5), document_id (optional)";

    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["query"] = new JsonObject { ["type"] = "string" },
            ["studio_id"] = new JsonObject { ["type"] = "string" },
            ["top_k"] = new JsonObject { ["type"] = "number" },
            ["document_id"] = new JsonObject { ["type"] = "string", ["description"] = "Tim kiem trong tai lieu cu the (optional)" }
        },
        ["required"] = new JsonArray { "query", "studio_id" }
    };

    public SearchStudioDocumentsTool(
        IVectorDatabaseService qdrantService,
        IEmbeddingService embeddingService,
        IStudioRepository studioRepository,
        ILogger<SearchStudioDocumentsTool> logger)
    {
        _qdrantService = qdrantService;
        _embeddingService = embeddingService;
        _studioRepository = studioRepository;
        _logger = logger;
    }

    private static string? Js(JsonNode? n) => n?.GetValue<string>();
    private static int Ji(JsonNode? n) => n == null ? 0 : n.GetValue<int>();
    private static Guid? Jg(JsonNode? n) =>
        string.IsNullOrWhiteSpace(Js(n)) ? null : Guid.TryParse(Js(n), out var g) ? g : null;

    public bool ValidateParameters(JsonObject p) =>
        !string.IsNullOrWhiteSpace(Js(p["query"])) &&
        Guid.TryParse(Js(p["studio_id"]), out _);

    public async Task<AIQueryResult> ExecuteAsync(
        AIQueryContext context, JsonObject parameters, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var query = Js(parameters["query"])!;
            var studioId = Guid.Parse(Js(parameters["studio_id"])!);
            var topK = Ji(parameters["top_k"]);
            var documentId = Jg(parameters["document_id"]);
            if (topK <= 0) topK = 5;

            _logger.LogInformation("[TOOL-START] search_studio_documents | query={Query} studioId={StudioId} topK={TopK}",
                query, studioId, topK);

            // Get all groups in this studio
            var groups = await _studioRepository.GetGroupsByStudioIdAsync(studioId);

            if (groups.Count == 0)
            {
                sw.Stop();
                _logger.LogWarning("[TOOL-EMPTY] search_studio_documents | query={Query} studioId={StudioId} — No groups in studio",
                    query, studioId);
                return AIQueryResult.Success(new JsonObject
                {
                    ["query"] = query,
                    ["documents"] = new JsonArray(),
                    ["total_found"] = 0,
                    ["groups_searched"] = 0,
                    ["qdrant_reachable"] = true,
                    ["summary"] = context.Language.ToLower() == "en"
                        ? "No groups found in this studio"
                        : "Khong co nhom nao trong studio"
                }, sw.ElapsedMilliseconds);
            }

            var groupIds = groups.Select(g => g.GroupId).ToList();
            var groupDict = groups.ToDictionary(g => g.GroupId);

            // Embed query
            var queryVector = await _embeddingService.GenerateEmbeddingAsync(query);

            // Search across all studio groups
            var results = await _qdrantService.SearchVectorsMultiGroupAsync(
                queryVector, topK, groupIds, documentId, cancellationToken);

            _logger.LogInformation("[TOOL-QDRANT] search_studio_documents | resultsCount={Count} elapsedMs={Ms}",
                results.Count, sw.ElapsedMilliseconds);

            // Build response with group info
            var docs = results.Select(r =>
            {
                var docGroupIdStr = r.Payload.GetValueOrDefault("groupId")?.ToString();
                var docGroupId = docGroupIdStr != null && Guid.TryParse(docGroupIdStr, out var g) ? g : Guid.Empty;
                var docGroup = groupDict.GetValueOrDefault(docGroupId);

                return new JsonObject
                {
                    ["document_id"] = r.Payload.GetValueOrDefault("documentId")?.ToString() ?? "",
                    ["file_name"] = r.Payload.GetValueOrDefault("fileName")?.ToString() ?? "",
                    ["content"] = r.Payload.GetValueOrDefault("content")?.ToString() ?? "",
                    ["relevance_score"] = Math.Round(r.Score, 4),
                    ["group_id"] = docGroupIdStr ?? "",
                    ["group_name"] = docGroup?.GroupName ?? "Unknown"
                };
            }).ToList();

            sw.Stop();
            bool isEnglish = context.Language.ToLower() == "en";

            if (docs.Count == 0)
            {
                _logger.LogWarning("[TOOL-EMPTY] search_studio_documents | query={Query} studioId={StudioId} — No documents found across {GroupCount} groups",
                    query, studioId, groups.Count);
            }
            else
            {
                _logger.LogInformation("[TOOL-SUCCESS] search_studio_documents | docsReturned={Count} elapsedMs={Ms}",
                    docs.Count, sw.ElapsedMilliseconds);
            }

            return AIQueryResult.Success(new JsonObject
            {
                ["query"] = query,
                ["documents"] = new JsonArray(docs.ToArray()),
                ["total_found"] = docs.Count,
                ["groups_searched"] = groups.Count,
                ["qdrant_reachable"] = true,
                ["summary"] = isEnglish
                    ? $"Found {docs.Count} relevant chunks across {groups.Count} groups"
                    : $"Tim thay {docs.Count} doan noi dung trong {groups.Count} nhom"
            }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "[TOOL-ERROR] search_studio_documents | query={Query} — Unexpected error", Js(parameters["query"]));
            return AIQueryResult.Error($"search_studio_documents failed: {ex.Message}");
        }
    }
}
