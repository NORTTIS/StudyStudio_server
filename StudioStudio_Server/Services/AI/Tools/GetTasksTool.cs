using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.AI.Tools.Interfaces;

namespace StudioStudio_Server.Services.AI.Tools;

public class GetTasksTool : IAITool
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 20;

    private readonly ITaskRepository _taskRepository;
    private readonly IGroupParticipantRepository _participantRepository;
    private readonly ILogger<GetTasksTool> _logger;

    public string Name => "get_tasks";
    public string Description => "Return a group's task list based on the user's intent. "
        + "This tool supports two different modes: keyword search and structured filtering. "
        + "Use query/search only for a specific keyword, task title fragment, or description fragment that the user explicitly wants to find. "
        + "For generic list requests such as 'task list', 'all tasks', 'group tasks', 'show me the tasks in this group', or for filter-only questions, keep query/search empty and use the structured filter fields instead. "
        + "Owner and Moderator roles can view all tasks in the group. Member roles can only view tasks assigned to the current user. "
        + "Filter semantics: status is an exact status-name filter; status_category groups tasks by lifecycle state (NotStarted, InProgress, Completed); priority is an exact match filter; min_priority means 'this level and above'; severity is an exact match filter; min_severity means 'this level and above'. "
        + "Pagination semantics: page selects the page index, page_size selects the number of tasks returned per page. "
        + $"Default page is 1 and default page_size is {DefaultPageSize}. Maximum page_size is {MaxPageSize}. "
        + "group_id is injected by the system and must not be provided by the LLM.";

    public JsonObject ParametersSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["query"] = new JsonObject { ["type"] = "string", ["description"] = "Use only for explicit keyword search across task title and description. Leave empty when the user is simply asking for a task list or only applying filters." },
            ["search"] = new JsonObject { ["type"] = "string", ["description"] = "Alias of query. Apply the exact same rule: only use it for explicit keyword search, never for generic list requests." },
            ["status"] = new JsonObject { ["type"] = "string", ["description"] = "Exact status-name filter. Use when the user mentions a concrete status label that exists in the group workflow." },
            ["status_category"] = new JsonObject { ["type"] = "string", ["description"] = "High-level workflow filter. Use NotStarted for tasks that have not started, InProgress for tasks that are underway, and Completed for finished tasks." },
            ["priority"] = new JsonObject { ["type"] = "string", ["description"] = "Exact priority match. Use this only when the user wants one specific priority level: Low, Medium, or High." },
            ["min_priority"] = new JsonObject { ["type"] = "string", ["description"] = "Threshold priority filter. Use this when the user says 'X and above', 'from X upward', or 'at least X'." },
            ["severity"] = new JsonObject { ["type"] = "string", ["description"] = "Exact severity match. Use this only when the user wants one specific severity level: Minor, Moderate, Major, or Critical." },
            ["min_severity"] = new JsonObject { ["type"] = "string", ["description"] = "Threshold severity filter. Use this when the user says 'X and above', 'from X upward', or 'at least X'." },
            ["page"] = new JsonObject { ["type"] = "number", ["description"] = "Requested page number for pagination. Use page 1 unless the user explicitly asks to see later pages or a follow-up requests the next page." },
            ["page_size"] = new JsonObject { ["type"] = "number", ["description"] = $"Number of tasks per page. Use the default value unless the user explicitly asks for a different page size. Maximum is {MaxPageSize}." },
            ["group_id"] = new JsonObject { ["type"] = "string", ["description"] = "Automatically injected by the system for the current group context. The LLM must not set this manually." }
        },
        ["required"] = new JsonArray()
    };

    public GetTasksTool(
        ITaskRepository taskRepository,
        IGroupParticipantRepository participantRepository,
        ILogger<GetTasksTool> logger)
    {
        _taskRepository = taskRepository;
        _participantRepository = participantRepository;
        _logger = logger;
    }

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

    private static bool ShouldUseAsSearchQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return false;

        var q = NormalizeText(query).Trim();

        // Generic listing intents should not become SQL LIKE filters
        if (q is "tat ca task" or "tat ca cac task" or "tat ca cong viec" or "tat ca cac cong viec" or "toan bo task" or "toan bo cong viec" or "all tasks" or "list tasks" or "danh sach task")
            return false;

        if ((q.Contains("danh sach") || q.Contains("liet ke") || q.Contains("xem danh sach") || q.Contains("lay danh sach") || q.Contains("hien thi") || q.Contains("xem"))
            && (q.Contains("task") || q.Contains("cong viec")))
        {
            return false;
        }

        if ((q.Contains("tat ca") || q.Contains("toan bo") || q.Contains("cac task") || q.Contains("cac cong viec") || q.Contains("cua nhom") || q.Contains("trong nhom"))
            && (q.Contains("task") || q.Contains("cong viec")))
            return false;

        // Filter intents (priority/severity/status) should use structured params, not full-text search
        if (q.Contains("loc") || q.Contains("filter"))
            return false;

        if (q.Contains("uu tien") || q.Contains("priority") || q.Contains("khan cap") || q.Contains("severity") || q.Contains("trang thai") || q.Contains("status"))
            return false;

        // Short/noisy strings are not good search inputs
        if (q.Length < 3)
            return false;

        return true;
    }

    private static TaskPriority? ParsePriority(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim().ToLower() switch
        {
            "low" or "thap" or "thấp" => TaskPriority.Low,
            "medium" or "trungbinh" or "trung binh" or "trung bình" => TaskPriority.Medium,
            "high" or "cao" => TaskPriority.High,
            _ => null
        };
    }

    private static TaskSeverity? ParseSeverity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim().ToLower() switch
        {
            "minor" => TaskSeverity.Minor,
            "moderate" => TaskSeverity.Moderate,
            "major" => TaskSeverity.Major,
            "critical" => TaskSeverity.Critical,
            _ => null
        };
    }

    private static string? NormalizeStatusCategory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        return value.Trim().ToLower() switch
        {
            "completed" or "done" or "hoanthanh" or "hoan thanh" => "Completed",
            "inprogress" or "in_progress" or "doing" or "danglam" or "dang lam" => "InProgress",
            "notstarted" or "not_started" or "todo" or "to do" or "chuabatdau" or "chua bat dau" => "NotStarted",
            _ => null
        };
    }

    public bool ValidateParameters(JsonObject parameters) => true;

    public async Task<AIQueryResult> ExecuteAsync(AIQueryContext context, JsonObject parameters, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (!context.GroupId.HasValue)
                return AIQueryResult.Error("Khong co group_id - chi hoat dong trong group context");

            var groupId = context.GroupId.Value;

            if (!await _participantRepository.IsUserInGroupAsync(groupId, context.UserId))
                return AIQueryResult.Error("Ban khong co quyen truy cap nhom nay");

            // Role check: Owner/Moderator thay tat ca, Member chi task duoc assign
            var role = await _participantRepository.GetGroupRoleByUserIdAsync(context.UserId, groupId);
            if (role == GroupRole.Commenter || role == GroupRole.Viewer)
                return AIQueryResult.Error("Ban khong co quyen su dung AI nhom nay");

            var searchQuery = Js(parameters["query"]);
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                searchQuery = Js(parameters["search"]);
            }

            if (!ShouldUseAsSearchQuery(searchQuery))
            {
                searchQuery = null;
            }

            var statusKeyword = Js(parameters["status"]);
            var statusCategory = NormalizeStatusCategory(Js(parameters["status_category"]));
            var priority = ParsePriority(Js(parameters["priority"]));
            var minPriority = ParsePriority(Js(parameters["min_priority"]));
            var severity = ParseSeverity(Js(parameters["severity"]));
            var minSeverity = ParseSeverity(Js(parameters["min_severity"]));
            var page = Ji(parameters["page"]);
            var pageSize = Ji(parameters["page_size"]);

            if (page <= 0) page = 1;
            if (pageSize <= 0) pageSize = DefaultPageSize;
            if (pageSize > MaxPageSize) pageSize = MaxPageSize;

            // Owner/Moderator: lay tat ca task; Member: chi task duoc assign
            Guid? assigneeId = (role == GroupRole.Owner || role == GroupRole.Moderator) ? null : context.UserId;

            var (tasks, total) = await _taskRepository.GetGroupTasksWithFiltersAsync(
                groupId, page, pageSize, searchQuery, assigneeId, null, priority, severity, null, null, null, null, "dueDate", true, statusKeyword, statusCategory, minPriority, minSeverity);

            var filteredCountOnCurrentPage = tasks.Count;
            var totalPages = (int)Math.Ceiling(total / (double)pageSize);
            if (totalPages <= 0) totalPages = 1;

            var taskList = tasks;
            var formattedTasks = taskList.Select(t => new JsonObject
            {
                ["id"] = t.TaskId.ToString(),
                ["title"] = t.Title ?? "",
                ["status"] = t.GroupStatus?.StatusName ?? "Khong co trang thai",
                ["status_category"] = t.Progress >= 100 ? "Completed"
                    : t.Progress > 0 ? "InProgress"
                    : "NotStarted",
                ["priority"] = t.Priority.ToString(),
                ["progress"] = t.Progress,
                ["group_id"] = t.GroupId.ToString(),
                ["due_date"] = t.DueDate?.ToString("yyyy-MM-dd HH:mm") ?? "",
                ["assignee_name"] = t.Owner != null ? $"{t.Owner.FirstName} {t.Owner.LastName}".Trim() : "Unassigned",
                ["is_completed"] = t.Progress >= 100,
                ["is_overdue"] = t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow && t.Progress < 100,
                ["severity"] = t.Severity.ToString(),
                ["estimated_hours"] = t.EstimatedHours.HasValue ? JsonValue.Create(t.EstimatedHours.Value) : null,
                ["actual_hours"] = t.ActualHours.HasValue ? JsonValue.Create(t.ActualHours.Value) : null
            }).ToList();

            sw.Stop();
            
            var result = AIQueryResult.Success(new JsonObject
            {
                ["tasks"] = new JsonArray(formattedTasks.ToArray()),
                ["search_query"] = searchQuery ?? "",
                ["status_filter"] = statusKeyword ?? "",
                ["status_category_filter"] = statusCategory ?? "",
                ["priority_filter"] = priority?.ToString() ?? "",
                ["min_priority_filter"] = minPriority?.ToString() ?? "",
                ["severity_filter"] = severity?.ToString() ?? "",
                ["min_severity_filter"] = minSeverity?.ToString() ?? "",
                ["total"] = total,
                ["returned"] = formattedTasks.Count,
                ["current_page"] = page,
                ["page_size"] = pageSize,
                ["total_pages"] = totalPages,
                ["has_next_page"] = page < totalPages,
                ["has_previous_page"] = page > 1,
                ["filtered_count_on_current_page"] = filteredCountOnCurrentPage,
                ["scope"] = (role == GroupRole.Owner || role == GroupRole.Moderator) ? "all_tasks" : "assigned_only"
            }, sw.ElapsedMilliseconds);
            
            // Log data size info for context tracking
            var resultJson = result.ToJson();
            
            _logger.LogInformation(
                "[TASKS-RESULT] query={Query} statusKeyword={StatusKeyword} statusCategory={StatusCategory} priority={Priority} minPriority={MinPriority} severity={Severity} minSeverity={MinSeverity} total={Total} returned={Returned} page={Page}/{TotalPages} pageSize={PageSize} contextSize={CharCount} (full data included)",
                searchQuery ?? "", statusKeyword ?? "", statusCategory ?? "", priority?.ToString() ?? "", minPriority?.ToString() ?? "", severity?.ToString() ?? "", minSeverity?.ToString() ?? "", total, formattedTasks.Count, page, totalPages, pageSize, resultJson.Length);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetTasksTool error");
            return AIQueryResult.Error("Da xay ra loi khi lay danh sach cong viec.");
        }
    }
}
