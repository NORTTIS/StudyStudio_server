using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.AI.Tools.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services.AI.Tools;

/// <summary>
/// Search documents in a group using vector similarity (Hybrid RAG)
/// Validates: group membership, query not empty
/// Returns: Top-K relevant document chunks with relevance scores
/// </summary>
public class SearchDocumentsTool : IAITool
{
    private readonly IVectorDatabaseService _qdrantService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IGroupParticipantRepository _participantRepository;
    private readonly ILogger<SearchDocumentsTool> _logger;

    public string Name => "search_documents";
    public string Description => "Tim kiem noi dung trong tai lieu cua nhom. "
        + "Parameters: query (bat buoc, cau hoi/tu khoa tim kiem), top_k (optional, mac dinh 3). "
        + "group_id tu dong lay tu he thong.";

    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["query"] = new JsonObject { ["type"] = "string", ["description"] = "Cau hoi/tu khoa tim kiem (bat buoc)" },
            ["top_k"] = new JsonObject { ["type"] = "number", ["description"] = "So ket qua toi da (default 3)" }
        },
        ["required"] = new JsonArray { "query" }
    };

    public SearchDocumentsTool(
        IVectorDatabaseService qdrantService,
        IEmbeddingService embeddingService,
        IGroupParticipantRepository participantRepository,
        ILogger<SearchDocumentsTool> logger)
    {
        _qdrantService = qdrantService;
        _embeddingService = embeddingService;
        _participantRepository = participantRepository;
        _logger = logger;
    }

    private static string? Js(JsonNode? n) => n?.GetValue<string>();
    private static int Ji(JsonNode? n) => n == null ? 0 : n.GetValue<int>();

    public bool ValidateParameters(JsonObject p) =>
        !string.IsNullOrWhiteSpace(Js(p["query"]));

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
            if (topK <= 0) topK = 3;

            // Permission check
            if (!await _participantRepository.IsUserInGroupAsync(groupId, context.UserId))
                return AIQueryResult.Error("Ban khong co quyen truy cap nhom nay");

            // Embed query
            var queryVector = await _embeddingService.GenerateEmbeddingAsync(query);

            // Search Qdrant
            var results = await _qdrantService.SearchVectorsAsync(
                queryVector, topK, groupId, cancellationToken);

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
            return AIQueryResult.Success(new JsonObject
            {
                ["query"] = query,
                ["documents"] = new JsonArray(docs.ToArray()),
                ["total_found"] = docs.Count,
                ["summary"] = isEnglish
                    ? $"Found {docs.Count} relevant document chunks"
                    : $"Tim thay {docs.Count} doan noi dung lien quan"
            }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SearchDocumentsTool error");
            return AIQueryResult.Error("Da xay ra loi khi tim kiem tai lieu");
        }
    }
}
