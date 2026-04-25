using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Interfaces;
using StudioStudio_Server.Services.AI.Models;

namespace StudioStudio_Server.Services.AI.Tools;

/// <summary>
/// Tool để lấy công việc được assign từ các nhóm của user trong personal scope.
/// Scope: Personal AI (UserId only)
/// </summary>
public class GetPersonalGroupTaskTool(
    ITaskRepository taskRepository,
    ILogger<GetPersonalGroupTaskTool> logger) : IAITool
{
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 20;

    public string Name => "get_personal_group_task";
    public string Description => "Lay danh sach cong viec duoc assign tu tat ca cac nhom cua nguoi dung. Khong can group_id. Ho tro query/search de tim theo ten cong viec hoac mo ta.";
    public string? PlanningHint => "Khi user hoi chi tiet cong viec duoc giao tu nhom theo ten, tim task theo ten, hoac muon xem mot cong viec cu the trong cac nhom, hay dung query/search.";

    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["query"] = new JsonObject { ["type"] = "string", ["description"] = "Tu khoa tim theo ten cong viec hoac mo ta. Dung khi user noi ro ten cong viec can xem." },
            ["search"] = new JsonObject { ["type"] = "string", ["description"] = "Alias cua query. Dung cung quy tac voi query." },
            ["page"] = new JsonObject { ["type"] = "number", ["description"] = "Trang hien tai (default 1)" },
            ["page_size"] = new JsonObject { ["type"] = "number", ["description"] = "So luong task tren moi trang (default 10, max 20)" }
        },
        ["required"] = new JsonArray()
    };

    private static string? Js(JsonNode? n) => n?.GetValue<string>();
    private static int Ji(JsonNode? n) => n == null ? 0 : n.GetValue<int>();

    private static string NormalizeText(string input)
    {
        var formD = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);

        foreach (var ch in formD)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            sb.Append(char.ToLowerInvariant(ch));
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static bool MatchesSearch(string? text, string normalizedQuery)
        => !string.IsNullOrWhiteSpace(text) && NormalizeText(text).Contains(normalizedQuery, StringComparison.Ordinal);

    public bool ValidateParameters(JsonObject parameters) => true;

    public async Task<AIQueryResult> ExecuteAsync(AIQueryContext context, JsonObject parameters, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var query = Js(parameters["query"]);
            if (string.IsNullOrWhiteSpace(query))
            {
                query = Js(parameters["search"]);
            }

            var page = Ji(parameters["page"]);
            var pageSize = Ji(parameters["page_size"]);

            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = DefaultPageSize;
            if (pageSize > MaxPageSize) pageSize = MaxPageSize;

            var now = DateTime.UtcNow;

            var assignedTasks = await taskRepository.GetAssignedGroupTasksByUserAsync(context.UserId);
            if (!string.IsNullOrWhiteSpace(query))
            {
                var normalizedQuery = NormalizeText(query).Trim();
                assignedTasks = assignedTasks
                    .Where(t => MatchesSearch(t.Title, normalizedQuery) || MatchesSearch(t.Description, normalizedQuery))
                    .ToList();
            }

            var totalAssigned = assignedTasks.Count;
            var totalPages = (int)Math.Ceiling(totalAssigned / (double)pageSize);
            if (totalPages <= 0) totalPages = 1;

            if (page > totalPages) page = totalPages;

            var pagedTasks = assignedTasks
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var result = pagedTasks.Select(t =>
            {
                var isCompleted = t.Progress >= 100;

                return new JsonObject
                {
                    ["id"] = t.TaskId.ToString(),
                    ["title"] = t.Title,
                    ["description"] = t.Description ?? "",
                    ["status"] = t.GroupStatus?.StatusName ?? "Unknown",
                    ["priority"] = t.Priority.ToString(),
                    ["progress"] = t.Progress,
                    ["due_date"] = t.DueDate?.ToString("yyyy-MM-dd HH:mm") ?? "",
                    ["is_completed"] = isCompleted,
                    ["completed_at"] = t.CompletedAt?.ToString("yyyy-MM-dd HH:mm") ?? "",
                    ["is_overdue"] = t.DueDate.HasValue && t.DueDate.Value < now && !isCompleted,
                    ["source"] = "group",
                    ["group_name"] = t.Group?.GroupName ?? "",
                    ["created_at"] = t.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                    ["severity"] = t.Severity.ToString(),
                    ["estimated_hours"] = t.EstimatedHours.HasValue ? JsonValue.Create(t.EstimatedHours.Value) : null,
                    ["actual_hours"] = t.ActualHours.HasValue ? JsonValue.Create(t.ActualHours.Value) : null
                };
            }).ToList();

            sw.Stop();
            var tasksArray = new JsonArray();
            foreach (var item in result)
            {
                tasksArray.Add(item);
            }

            return AIQueryResult.Success(new JsonObject
            {
                ["tasks"] = tasksArray,
                ["total"] = totalAssigned,
                ["group_count"] = totalAssigned,
                ["personal_count"] = 0,
                ["current_page"] = page,
                ["page_size"] = pageSize,
                ["total_pages"] = totalPages,
                ["has_next_page"] = page < totalPages,
                ["has_previous_page"] = page > 1,
                ["returned"] = result.Count,
                ["search_query"] = query ?? "",
                ["summary"] = string.IsNullOrWhiteSpace(query)
                    ? $"Ban co {totalAssigned} cong viec duoc assign tu cac nhom. Hien thi {result.Count} / {totalAssigned} (trang {page}/{totalPages})."
                    : $"Tim thay {totalAssigned} cong viec duoc assign tu nhom phu hop voi tu khoa '{query}'. Hien thi {result.Count} / {totalAssigned} (trang {page}/{totalPages})."
            }, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetPersonalGroupTaskTool error for UserId={UserId}", context.UserId);
            return AIQueryResult.Error("Da xay ra loi khi lay danh sach cong viec tu nhom.");
        }
    }
}
