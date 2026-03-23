using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.AI.Tools.Interfaces;

namespace StudioStudio_Server.Services.AI.Tools;

[DebuggerStepThrough]
public class GetStorageUsageTool : IAITool
{
    private readonly IStudioRepository _studioRepository;
    private readonly ILogger<GetStorageUsageTool> _logger;

    public string Name => "get_storage_usage";
    public string Description => "Kiem tra dung luong luu tru cua Studio. Parameters: studio_id (required)";
    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["studio_id"] = new JsonObject { ["type"] = "string" }
        },
        ["required"] = new JsonArray { "studio_id" }
    };

    public GetStorageUsageTool(
        IStudioRepository studioRepository,
        ILogger<GetStorageUsageTool> logger)
    {
        _studioRepository = studioRepository;
        _logger = logger;
    }

    private static string? Js(JsonNode? n) => n?.GetValue<string>();

    public bool ValidateParameters(JsonObject p) =>
        Guid.TryParse(Js(p["studio_id"]), out _);

    public async Task<AIQueryResult> ExecuteAsync(AIQueryContext context, JsonObject parameters, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // Auto-inject studio_id from context if LLM didn't provide it
            if (!parameters.ContainsKey("studio_id") && context.StudioId.HasValue)
            {
                parameters["studio_id"] = JsonValue.Create(context.StudioId.Value.ToString());
            }

            if (!Guid.TryParse(Js(parameters["studio_id"]), out var studioId))
                return AIQueryResult.Error("Invalid studio_id");

            if (context.StudioId.HasValue && context.StudioId.Value != studioId)
                return AIQueryResult.Error("Ban khong co quyen truy cap Studio nay");

            var studio = await _studioRepository.GetByIdAsync(studioId);
            if (studio == null)
                return AIQueryResult.Error("Khong tim thay Studio");

            // Default storage quota: 1GB (1073741824 bytes)
            const long defaultQuotaBytes = 1073741824L;

            // Placeholder for actual storage usage — would be replaced with document repository call
            var storageUsedBytes = 0L;
            var storageQuotaBytes = defaultQuotaBytes;
            var tier = "free";

            var usagePercentage = storageQuotaBytes > 0
                ? Math.Round((double)storageUsedBytes / storageQuotaBytes * 100, 2)
                : 0.0;

            sw.Stop();
            return AIQueryResult.Success(new JsonObject
            {
                ["studio_id"] = studioId.ToString(),
                ["storage_used_bytes"] = storageUsedBytes,
                ["storage_used_display"] = FormatBytes(storageUsedBytes),
                ["storage_quota_bytes"] = storageQuotaBytes,
                ["storage_quota_display"] = FormatBytes(storageQuotaBytes),
                ["usage_percentage"] = usagePercentage,
                ["tier"] = tier,
                ["generated_at"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetStorageUsageTool error");
            return AIQueryResult.Error("Da xay ra loi khi kiem tra dung luong luu tru");
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{Math.Round(bytes / 1024.0, 2)} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{Math.Round(bytes / (1024.0 * 1024), 2)} MB";
        return $"{Math.Round(bytes / (1024.0 * 1024 * 1024), 2)} GB";
    }
}
