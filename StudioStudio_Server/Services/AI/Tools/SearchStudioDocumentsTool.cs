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
        + "Parameters: query (bat buoc), studio_id (bat buoc), top_k (optional, mac dinh 5)";

    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["query"] = new JsonObject { ["type"] = "string" },
            ["studio_id"] = new JsonObject { ["type"] = "string" },
            ["top_k"] = new JsonObject { ["type"] = "number" }
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
            if (topK <= 0) topK = 5;

            // Get all groups in this studio
            var groups = await _studioRepository.GetGroupsByStudioIdAsync(studioId);

            if (groups.Count == 0)
            {
                sw.Stop();
                return AIQueryResult.Success(new JsonObject
                {
                    ["query"] = query,
                    ["documents"] = new JsonArray(),
                    ["total_found"] = 0,
                    ["groups_searched"] = 0,
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
                queryVector, topK, groupIds, cancellationToken);

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
            return AIQueryResult.Success(new JsonObject
            {
                ["query"] = query,
                ["documents"] = new JsonArray(docs.ToArray()),
                ["total_found"] = docs.Count,
                ["groups_searched"] = groups.Count,
                ["summary"] = isEnglish
                    ? $"Found {docs.Count} relevant chunks across {groups.Count} groups"
                    : $"Tim thay {docs.Count} doan noi dung trong {groups.Count} nhom"
            }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SearchStudioDocumentsTool error");
            return AIQueryResult.Error("Da xay ra loi khi tim kiem tai lieu studio");
        }
    }
}
