using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using StudioStudio_Server.Configurations;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.AI.Tools.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services.AI;

/// <summary>
/// AIAgent - Bộ não AI có khả năng gọi tools để lấy data trực tiếp từ database
/// Sử dụng ReAct pattern (Reasoning + Acting)
/// </summary>
public class AIAgent
{
    private const int HardMaxToolCalls = 5;
    private const int MaxConsecutiveDecideWithoutExecution = 3;
    private const int MaxToolResultCharsForPrompt = 2500;
    private const int MaxArrayItemsForPrompt = 6;
    private const int MaxStringCharsForPrompt = 300;

    private readonly IAIToolRegistry _toolRegistry;
    private readonly IServiceProvider _serviceProvider;  // Resolve fresh tool instances per request
    private readonly ILLMService _llmService;
    private readonly ICacheService _cacheService;
    private readonly AIToolCacheService _toolCacheService;
    private readonly ILogger<AIAgent> _logger;
    private readonly AIAgentConfig _config;  // Configuration from appsettings

    // System prompt cho agent
    private readonly string _systemPromptVi;
    private readonly string _systemPromptEn;

    // Role-specific prompts
    private readonly string _personalSystemPromptVi;
    private readonly string _personalSystemPromptEn;
    private readonly string _ownerSystemPromptVi;
    private readonly string _ownerSystemPromptEn;

    // Token usage tracking across all LLM calls in a request
    private TokenUsage? _currentTokenUsage;

    /// <summary>
    /// Kiem tra xem parameters co query rong hoac null khong
    /// </summary>
    private bool HasEmptyQuery(JsonObject? p) =>
        p == null ||
        !p.TryGetPropertyValue("query", out var q) ||
        string.IsNullOrWhiteSpace(q?.GetValue<string>());

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

    private static bool ContainsAny(string input, params string[] phrases)
    {
        foreach (var phrase in phrases)
        {
            if (input.Contains(phrase, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static JsonObject NormalizeGetTasksParameters(string userQuestion, JsonObject? parameters)
    {
        var normalizedParameters = parameters ?? new JsonObject();
        var question = NormalizeText(userQuestion);

        if (string.IsNullOrWhiteSpace(question))
        {
            return normalizedParameters;
        }

        var mentionsPriority = question.Contains("uu tien", StringComparison.Ordinal) || question.Contains("priority", StringComparison.Ordinal);
        var mentionsSeverity = question.Contains("muc do", StringComparison.Ordinal)
            || question.Contains("severity", StringComparison.Ordinal)
            || question.Contains("khan cap", StringComparison.Ordinal)
            || question.Contains("do khan cap", StringComparison.Ordinal)
            || question.Contains("urgency", StringComparison.Ordinal);
        var mentionsStatus = question.Contains("trang thai", StringComparison.Ordinal)
            || question.Contains("status", StringComparison.Ordinal)
            || question.Contains("hoan thanh", StringComparison.Ordinal)
            || question.Contains("dang lam", StringComparison.Ordinal)
            || question.Contains("chua bat dau", StringComparison.Ordinal)
            || question.Contains("todo", StringComparison.Ordinal);

        if (!HasTaskFilterParameters(normalizedParameters) && (mentionsPriority || mentionsSeverity || mentionsStatus))
        {
            normalizedParameters["query"] = null;
            normalizedParameters["search"] = null;
        }

        if (mentionsPriority)
        {
            if (ContainsAny(question, "tu muc cao tro len", "muc cao tro len", "high and above", "high or higher", "at least high", "from high upward"))
            {
                normalizedParameters["min_priority"] = JsonValue.Create("High");
            }
            else if (ContainsAny(question, "tu muc trung binh tro len", "muc trung binh tro len", "medium and above", "medium or higher", "at least medium", "from medium upward"))
            {
                normalizedParameters["min_priority"] = JsonValue.Create("Medium");
            }
            else if (ContainsAny(question, "uu tien cao", "priority high", "priority cao"))
            {
                normalizedParameters["priority"] = JsonValue.Create("High");
            }
            else if (ContainsAny(question, "uu tien trung binh", "priority medium"))
            {
                normalizedParameters["priority"] = JsonValue.Create("Medium");
            }
            else if (ContainsAny(question, "uu tien thap", "priority low"))
            {
                normalizedParameters["priority"] = JsonValue.Create("Low");
            }
        }

        if (mentionsSeverity)
        {
            if (ContainsAny(question, "muc do cao tro len", "muc cao tro len", "tu muc cao tro len", "do khan cap cao tro len", "khan cap cao tro len", "severity high and above", "high or higher severity", "at least major", "from major upward"))
            {
                normalizedParameters["min_severity"] = JsonValue.Create("Major");
            }
            else if (ContainsAny(question, "muc do trung binh tro len", "muc trung binh tro len", "tu muc trung binh tro len", "do khan cap trung binh tro len", "khan cap trung binh tro len", "severity moderate and above", "medium or higher severity", "at least moderate", "from moderate upward"))
            {
                normalizedParameters["min_severity"] = JsonValue.Create("Moderate");
            }
            else if (ContainsAny(question, "muc do rat cao", "do khan cap rat cao", "khan cap rat cao", "severity critical", "critical severity"))
            {
                normalizedParameters["severity"] = JsonValue.Create("Critical");
            }
            else if (ContainsAny(question, "muc do cao", "do khan cap cao", "khan cap cao", "severity major"))
            {
                normalizedParameters["severity"] = JsonValue.Create("Major");
            }
            else if (ContainsAny(question, "muc do trung binh", "do khan cap trung binh", "khan cap trung binh", "severity moderate"))
            {
                normalizedParameters["severity"] = JsonValue.Create("Moderate");
            }
            else if (ContainsAny(question, "muc do thap", "do khan cap thap", "khan cap thap", "severity minor"))
            {
                normalizedParameters["severity"] = JsonValue.Create("Minor");
            }
        }

        if (mentionsStatus)
        {
            if (ContainsAny(question, "hoan thanh", "completed", "done", "finished"))
            {
                normalizedParameters["status_category"] = JsonValue.Create("Completed");
            }
            else if (ContainsAny(question, "dang lam", "in progress", "doing"))
            {
                normalizedParameters["status_category"] = JsonValue.Create("InProgress");
            }
            else if (ContainsAny(question, "chua bat dau", "not started", "todo"))
            {
                normalizedParameters["status_category"] = JsonValue.Create("NotStarted");
            }
        }

        return normalizedParameters;
    }

    private sealed record AIIntentAnalysis(
        string Category,
        bool RequiresTool,
        bool IsTaskIntent,
        bool IsDocumentIntent,
        bool IsFollowUp,
        string Summary);

    private sealed record AIFlowDecision(
        string StepName,
        AgentDecision Decision,
        JsonObject ToolParameters,
        bool IsAccepted = true,
        string ReviewState = "accepted",
        string? ReviewNote = null,
        string? SuggestedToolName = null,
        JsonObject? SuggestedParameters = null);

    private sealed record AIReviewVerdict(
        bool IsAccepted,
        string ReviewNote,
        string ReviewState,
        string? SuggestedToolName = null,
        JsonObject? SuggestedParameters = null);

    public AIAgent(
        IAIToolRegistry toolRegistry,
        IServiceProvider serviceProvider,
        ILLMService llmService,
        ICacheService cacheService,
        AIToolCacheService toolCacheService,
        ILogger<AIAgent> logger,
        IOptions<AIAgentConfig> configOptions)
    {
        _toolRegistry = toolRegistry;
        _serviceProvider = serviceProvider;
        _llmService = llmService;
        _cacheService = cacheService;
        _toolCacheService = toolCacheService;
        _logger = logger;
        _config = configOptions.Value;

        _systemPromptVi = GetSystemPromptVi();
        _systemPromptEn = GetSystemPromptEn();
        _personalSystemPromptVi = GetPersonalSystemPromptVi();
        _personalSystemPromptEn = GetPersonalSystemPromptEn();
        _ownerSystemPromptVi = GetOwnerSystemPromptVi();
        _ownerSystemPromptEn = GetOwnerSystemPromptEn();
    }

    private static AIIntentAnalysis AnalyzeIntent(string userQuestion, AIQueryContext context)
    {
        var normalizedQuestion = NormalizeText(userQuestion);

        var isFollowUp = normalizedQuestion.Contains("xem tiep", StringComparison.Ordinal)
            || normalizedQuestion.Contains("trang tiep", StringComparison.Ordinal)
            || normalizedQuestion == "next"
            || normalizedQuestion == "more";

        var isTaskIntent = normalizedQuestion.Contains("task", StringComparison.Ordinal)
            || normalizedQuestion.Contains("cong viec", StringComparison.Ordinal)
            || normalizedQuestion.Contains("deadline", StringComparison.Ordinal)
            || normalizedQuestion.Contains("priority", StringComparison.Ordinal)
            || normalizedQuestion.Contains("uu tien", StringComparison.Ordinal)
            || normalizedQuestion.Contains("severity", StringComparison.Ordinal)
            || normalizedQuestion.Contains("score", StringComparison.Ordinal)
            || normalizedQuestion.Contains("diem", StringComparison.Ordinal);

        var isDocumentIntent = normalizedQuestion.Contains("tai lieu", StringComparison.Ordinal)
            || normalizedQuestion.Contains("document", StringComparison.Ordinal)
            || normalizedQuestion.Contains("file", StringComparison.Ordinal)
            || normalizedQuestion.Contains("pdf", StringComparison.Ordinal)
            || normalizedQuestion.Contains("slide", StringComparison.Ordinal);

        var category = context.StudioId.HasValue
            ? "studio"
            : context.GroupId.HasValue
                ? (isTaskIntent ? "group-task" : isDocumentIntent ? "group-document" : "group-general")
                : "personal";

        var requiresTool = true;
        var summary = $"category={category}, taskIntent={isTaskIntent}, documentIntent={isDocumentIntent}, followUp={isFollowUp}";

        return new AIIntentAnalysis(category, requiresTool, isTaskIntent, isDocumentIntent, isFollowUp, summary);
    }

    private int GetEffectiveMaxToolCalls() => Math.Min(_config.MaxToolCalls, HardMaxToolCalls);

    private static JsonObject EnsureToolParameters(JsonObject? parameters) => parameters ?? new JsonObject();

    private static bool IsTaskTool(string toolName)
    {
        return toolName.Equals("get_tasks", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("get_group_stats", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("get_deadlines", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("get_members", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("get_group_performance", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("get_group_risk", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDocumentTool(string toolName)
    {
        return toolName.Equals("get_group_documents", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("search_documents", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPersonalTool(string toolName)
    {
        return toolName.Equals("get_personal_tasks", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("get_personal_deadlines", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("get_personal_stats", StringComparison.OrdinalIgnoreCase);
    }

    private static AIReviewVerdict ReviewToolFit(AIIntentAnalysis intent, AgentDecision decision)
    {
        if (string.IsNullOrWhiteSpace(decision.ToolName))
        {
            return new AIReviewVerdict(false, "Missing tool name", "wrong_tool");
        }

        var toolName = decision.ToolName;

        // Allow mixed intents (task + document in one question).
        // Reject only when the intent is purely document and a task tool is chosen.
        if (intent.IsDocumentIntent && !intent.IsTaskIntent && IsTaskTool(toolName))
        {
            return new AIReviewVerdict(false, "User asked about documents but planned a task tool", "get_group_documents");
        }

        // Reject only when the intent is purely task and a document tool is chosen.
        if (intent.IsTaskIntent && !intent.IsDocumentIntent && IsDocumentTool(toolName))
        {
            return new AIReviewVerdict(false, "User asked about tasks but planned a document tool", "get_tasks");
        }

        if (intent.Category == "personal" && !IsPersonalTool(toolName))
        {
            return new AIReviewVerdict(false, "Personal context should use personal tools", "get_personal_tasks");
        }

        if (intent.Category.StartsWith("group", StringComparison.OrdinalIgnoreCase) && IsPersonalTool(toolName))
        {
            return new AIReviewVerdict(false, "Group context should not use personal tools", "get_tasks");
        }

        return new AIReviewVerdict(true, "Tool fits user intent", toolName);
    }

    /// <summary>
    /// Trích xuất tên tài liệu từ câu hỏi của user
    /// Tìm các keyword như: "file", "document", "tài liệu", ".pdf", ".docx" v.v.
    /// </summary>
    private static List<string> ExtractDocumentNamesFromQuestion(string userQuestion)
    {
        if (string.IsNullOrWhiteSpace(userQuestion))
            return new();

        var docNames = new List<string>();

        // Bat cac ten file co extension pho bien trong cau hoi user
        // Vi du: "2003.txt", "bao-cao-cuoi-ky.pdf"
        var fileMatches = System.Text.RegularExpressions.Regex.Matches(
            userQuestion,
            @"[A-Za-z0-9_\-\. ]+\.(pdf|docx|xlsx|txt|pptx|doc|xls|ppt|jpg|png|jpeg)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (System.Text.RegularExpressions.Match match in fileMatches)
        {
            var value = match.Value.Trim();
            if (!string.IsNullOrWhiteSpace(value) && !docNames.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                docNames.Add(value);
            }
        }

        // Nếu không tìm thấy file extension, tìm các keyword "file" hoặc "tài liệu" theo sau bởi tên
        if (docNames.Count == 0)
        {
            // Pattern: "file XYZ", "tài liệu XYZ", "document XYZ"
            var keywordPatterns = new[] { @"file\s+([^\s,\.]+)", @"tài liệu\s+([^\s,\.]+)", @"document\s+([^\s,\.]+)" };
            foreach (var pattern in keywordPatterns)
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(
                    userQuestion,
                    pattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    if (m.Groups.Count > 1)
                    {
                        var name = m.Groups[1].Value.Trim();
                        if (!string.IsNullOrWhiteSpace(name) && !docNames.Contains(name))
                        {
                            docNames.Add(name);
                        }
                    }
                }
            }
        }

        return docNames;
    }

    /// <summary>
    /// Tìm documentId từ danh sách tài liệu dựa trên tên/keyword
    /// </summary>
    private static List<string> MatchDocumentNamesAndExtractIds(
        List<string> searchNames,
        AIQueryResult docListResult)
    {
        if (searchNames.Count == 0 || !docListResult.IsSuccess || docListResult.Data == null)
            return new();

        var matchedIds = new List<string>();

        // Lấy danh sách documents từ kết quả
        if (!docListResult.Data.TryGetPropertyValue("documents", out var docsNode) || docsNode is not JsonArray docs)
        {
            return new();
        }

        // Duyệt qua mỗi tên tìm kiếm
        foreach (var searchName in searchNames)
        {
            var searchNameLower = searchName.ToLower();

            // Duyệt qua danh sách documents để tìm match
            foreach (var doc in docs)
            {
                if (doc is not JsonObject docObj || !docObj.TryGetPropertyValue("file_name", out var fileNameNode))
                    continue;

                var fileName = fileNameNode?.GetValue<string>()?.ToLower() ?? "";

                // Match: exact match hoặc partial match
                if (fileName.Equals(searchNameLower) || 
                    fileName.Contains(searchNameLower) ||
                    searchNameLower.Contains(System.IO.Path.GetFileNameWithoutExtension(fileName)))
                {
                    // Chi dung document_id hop le (GUID) de filter Qdrant
                    string? docId = null;
                    if (docObj.TryGetPropertyValue("document_id", out var idNode))
                    {
                        var raw = idNode?.GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out var parsed))
                        {
                            docId = parsed.ToString();
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(docId) && !matchedIds.Contains(docId))
                    {
                        matchedIds.Add(docId);
                    }
                    break;
                }
            }
        }

        return matchedIds;
    }

    private static JsonObject MergeJsonObjects(JsonObject current, JsonObject updates)
    {
        var merged = current.DeepClone() as JsonObject ?? new JsonObject();

        foreach (var kv in updates)
        {
            merged[kv.Key] = kv.Value?.DeepClone();
        }

        return merged;
    }

    private static AIReviewVerdict ParseParameterReviewResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return new AIReviewVerdict(false, "Empty parameter review response", "wrong_tool");
        }

        var trimmed = response.Trim();
        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            trimmed = trimmed.Substring(start, end - start + 1);
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;

            var reviewState = root.TryGetProperty("review_state", out var stateNode)
                ? (stateNode.GetString() ?? string.Empty).Trim().ToLowerInvariant()
                : string.Empty;

            if (reviewState != "accepted" && reviewState != "needs_fix" && reviewState != "wrong_tool")
            {
                reviewState = root.TryGetProperty("is_accepted", out var acceptedNode) && acceptedNode.ValueKind == JsonValueKind.True
                    ? "accepted"
                    : "wrong_tool";
            }

            var isAccepted = reviewState != "wrong_tool";
            var reviewNote = root.TryGetProperty("review_note", out var noteNode) ? noteNode.GetString() ?? "" : "";
            var suggestedToolName = root.TryGetProperty("suggested_tool_name", out var toolNode) ? toolNode.GetString() : null;

            JsonObject? suggestedParameters = null;
            if (root.TryGetProperty("suggested_parameters", out var paramsNode) && paramsNode.ValueKind == JsonValueKind.Object)
            {
                suggestedParameters = JsonSerializer.Deserialize<JsonObject>(paramsNode.GetRawText());
            }

            return new AIReviewVerdict(isAccepted, reviewNote, reviewState, suggestedToolName, suggestedParameters);
        }
        catch
        {
            return new AIReviewVerdict(false, "Parameter review response was not valid JSON", "wrong_tool");
        }
    }

    private async Task<AIReviewVerdict> ReviewToolParametersAsync(
        string userQuestion,
        AIQueryContext context,
        IAITool tool,
        JsonObject parameters,
        CancellationToken cancellationToken)
    {
        var schemaJson = JsonSerializer.Serialize(tool.ParametersSchema, new JsonSerializerOptions { WriteIndented = true });
        var parametersJson = JsonSerializer.Serialize(parameters, new JsonSerializerOptions { WriteIndented = true });
        var language = context.Language.Equals("en", StringComparison.OrdinalIgnoreCase) ? "English" : "Vietnamese";

        var systemPrompt = language == "English"
            ? "You are a strict AI parameter reviewer for Study Studio tools. Return only raw JSON."
            : "Ban la bo loc tham so AI nghiem ngat cho cac tool cua Study Studio. Chi tra ve JSON thuan tuy.";

        var prompt = new StringBuilder();
        prompt.AppendLine(language == "English"
            ? "Review whether the proposed parameters fit the user's request and the tool schema."
            : "Hay danh gia xem cac parameter duoc de xuat co khop voi yeu cau nguoi dung va schema cua tool hay khong.");
        prompt.AppendLine(language == "English"
            ? "Return ONLY a JSON object with keys: review_state (accepted|needs_fix|wrong_tool), review_note (string), suggested_tool_name (string|null), suggested_parameters (object|null)."
            : "Chi tra ve mot JSON object voi cac key: review_state (accepted|needs_fix|wrong_tool), review_note (string), suggested_tool_name (string|null), suggested_parameters (object|null).");
        prompt.AppendLine(language == "English"
            ? "Use accepted when the parameters are already correct. Use needs_fix when the tool is right but the parameters can be corrected from the user request; provide corrected suggested_parameters. Use wrong_tool when the planned tool does not fit the user's intent."
            : "Dung accepted khi parameters da dung. Dung needs_fix khi tool dung nhung parameters co the sua tu cau hoi nguoi dung; hay dua ra suggested_parameters da chinh sua. Dung wrong_tool khi tool dang chon khong hop voi y dinh nguoi dung.");
        prompt.AppendLine(language == "English"
            ? "If review_state is wrong_tool, also suggest the better tool name."
            : "Neu review_state la wrong_tool, hay de xuat tool phu hop hon.");
        prompt.AppendLine();
        prompt.AppendLine(language == "English" ? "User question:" : "Cau hoi nguoi dung:");
        prompt.AppendLine(userQuestion);
        prompt.AppendLine();
        prompt.AppendLine(language == "English" ? "Tool name:" : "Ten tool:");
        prompt.AppendLine(tool.Name);
        prompt.AppendLine();
        prompt.AppendLine(language == "English" ? "Tool description:" : "Mo ta tool:");
        prompt.AppendLine(tool.Description);
        prompt.AppendLine();
        prompt.AppendLine(language == "English" ? "Tool schema:" : "Schema cua tool:");
        prompt.AppendLine(schemaJson);
        prompt.AppendLine();
        prompt.AppendLine(language == "English" ? "Current parameters:" : "Parameters hien tai:");
        prompt.AppendLine(parametersJson);

        var rawResponse = await _llmService.GenerateTextResponseAsync(systemPrompt, prompt.ToString(), string.Empty, cancellationToken);
        return ParseParameterReviewResponse(rawResponse);
    }

    private async Task<AIFlowDecision> ReviewPlannedToolAsync(string userQuestion, AIQueryContext context, AgentDecision decision, CancellationToken cancellationToken)
    {
        var reviewedParameters = EnsureToolParameters(decision.ToolParameters);

        if (string.IsNullOrWhiteSpace(decision.ToolName))
        {
            return new AIFlowDecision(
                StepName: "parameter-review",
                Decision: decision,
                ToolParameters: reviewedParameters,
                IsAccepted: false,
                ReviewState: "wrong_tool",
                ReviewNote: "Missing tool name");
        }

        var tool = _toolRegistry.GetTool(decision.ToolName);
        if (tool == null)
        {
            return new AIFlowDecision(
                StepName: "parameter-review",
                Decision: decision,
                ToolParameters: reviewedParameters,
                IsAccepted: false,
                ReviewState: "wrong_tool",
                ReviewNote: $"Unknown tool '{decision.ToolName}'",
                SuggestedToolName: decision.ToolName);
        }

        // get_tasks has complex parameter normalization - always review
        if (decision.ToolName.Equals("get_tasks", StringComparison.OrdinalIgnoreCase))
        {
            reviewedParameters = NormalizeGetTasksParameters(userQuestion, reviewedParameters);
            var verdict = await ReviewToolParametersAsync(userQuestion, context, tool, reviewedParameters, cancellationToken);
            if (verdict.SuggestedParameters != null)
            {
                reviewedParameters = MergeJsonObjects(reviewedParameters, verdict.SuggestedParameters);
            }
            return new AIFlowDecision(
                StepName: "parameter-review",
                Decision: decision,
                ToolParameters: reviewedParameters,
                IsAccepted: verdict.IsAccepted,
                ReviewState: verdict.ReviewState,
                ReviewNote: $"[{verdict.ReviewState}] {verdict.ReviewNote}",
                SuggestedToolName: verdict.SuggestedToolName,
                SuggestedParameters: verdict.SuggestedParameters);
        }

        // For other tools, only review if parameters look suspicious
        if (reviewedParameters.Count == 0)
        {
            // No parameters - tools like get_group_stats, get_members don't need params
            _logger.LogInformation("[PARAM-REVIEW] Tool {Tool} has no params, auto-accepting", decision.ToolName);
            return new AIFlowDecision(
                StepName: "parameter-review",
                Decision: decision,
                ToolParameters: reviewedParameters,
                IsAccepted: true,
                ReviewState: "accepted",
                ReviewNote: "No parameters required");
        }

        // Check if all params are empty/null (suspicious)
        var hasValidParams = false;
        foreach (var prop in reviewedParameters)
        {
            var node = prop.Value;
            if (node == null)
            {
                continue;
            }

            if (node is JsonValue value)
            {
                if (value.TryGetValue<string>(out var stringValue))
                {
                    if (!string.IsNullOrWhiteSpace(stringValue))
                    {
                        hasValidParams = true;
                        break;
                    }

                    continue;
                }

                // Non-string primitives (numbers/booleans) count as valid when present.
                hasValidParams = true;
                break;
            }

            // Objects/arrays count as valid when present.
            if (node is JsonObject || node is JsonArray)
            {
                hasValidParams = true;
                break;
            }
        }
        if (!hasValidParams)
        {
            // All params are null/empty - probably wrong, let LLM review
            var verdict = await ReviewToolParametersAsync(userQuestion, context, tool, reviewedParameters, cancellationToken);
            if (verdict.SuggestedParameters != null)
            {
                reviewedParameters = MergeJsonObjects(reviewedParameters, verdict.SuggestedParameters);
            }
            return new AIFlowDecision(
                StepName: "parameter-review",
                Decision: decision,
                ToolParameters: reviewedParameters,
                IsAccepted: verdict.IsAccepted,
                ReviewState: verdict.ReviewState,
                ReviewNote: $"[{verdict.ReviewState}] {verdict.ReviewNote}",
                SuggestedToolName: verdict.SuggestedToolName,
                SuggestedParameters: verdict.SuggestedParameters);
        }

        // Parameters look valid - skip expensive LLM review
        _logger.LogInformation("[PARAM-REVIEW] Tool {Tool} has valid params, skipping LLM review", decision.ToolName);
        return new AIFlowDecision(
            StepName: "parameter-review",
            Decision: decision,
            ToolParameters: reviewedParameters,
            IsAccepted: true,
            ReviewState: "accepted",
            ReviewNote: "Parameters validated (no LLM review needed)");
    }

    private bool ValidateToolResult(string toolName, AIQueryResult result, out string? validationNote)
    {
        validationNote = null;

        if (!result.IsSuccess)
        {
            validationNote = result.ErrorMessage ?? "tool failed";
            return false;
        }

        if (toolName.Equals("get_tasks", StringComparison.OrdinalIgnoreCase) && result.Data != null)
        {
            if (!result.Data.TryGetPropertyValue("tasks", out var tasksNode) || tasksNode is not JsonArray tasksArr)
            {
                validationNote = "get_tasks returned no tasks array";
                return false;
            }

            validationNote = $"validated_tasks={tasksArr.Count}";
        }

        return true;
    }

    private Task<AgentDecision> PlanNextActionAsync(
        string userQuestion,
        string systemPrompt,
        JsonObject toolsManifest,
        ToolExecutionHistory history,
        AIQueryContext context,
        CancellationToken cancellationToken,
        bool isContinuation = false,
        int consecutiveDecideWithoutExecution = 0)
    {
        return DecideActionAsync(
            userQuestion,
            systemPrompt,
            toolsManifest,
            history,
            context,
            cancellationToken,
            isContinuation,
            consecutiveDecideWithoutExecution);
    }

    /// <summary>
    /// Xử lý câu hỏi với khả năng gọi tools
    /// </summary>
    public async Task<AIAgentResult> ProcessAsync(
        string userQuestion,
        AIQueryContext context,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var history = new ToolExecutionHistory();
        var reasoningSteps = new List<string>();

        // Reset token usage tracking for this request
        _currentTokenUsage = null;

        try
        {
            // Step 1: Intent understanding
            var intent = AnalyzeIntent(userQuestion, context);
            reasoningSteps.Add($"[INTENT] {intent.Summary}");

            var paginationState = await GetTaskPaginationStateAsync(context);
            var isTaskFollowup = IsTaskPaginationFollowup(userQuestion);

            if (isTaskFollowup)
            {
                reasoningSteps.Add("[FOLLOWUP] Detected task pagination follow-up. Use cached task state.");
            }

            if (isTaskFollowup)
            {
                await TryExecuteTaskFollowupAsync(userQuestion, context, history, reasoningSteps, paginationState, cancellationToken);
            }

            // Doc intent: luon nap danh sach tai lieu + semantic search som
            // de co context va document_id filter truoc khi vao vong plan chinh.
            if (intent.IsDocumentIntent && !intent.IsTaskIntent)
            {
                await AutoFetchDocumentContextAsync(userQuestion, history, reasoningSteps, context, cancellationToken);
            }

            reasoningSteps.Add($"[ANALYZE] Question={userQuestion}");

            // Step 2: Tool planning - chỉ lấy tools phù hợp với role của user
            var toolsManifest = _toolRegistry.GetToolsManifestForContext(context);

            // Step 3: Plan the first action using LLM + context
            var systemPrompt = GetRoleSystemPrompt(context);

            var decision = await PlanNextActionAsync(
                userQuestion,
                systemPrompt,
                toolsManifest,
                history,
                context,
                cancellationToken);

            var maxToolCalls = GetEffectiveMaxToolCalls();
            var parameterReviewFailures = 0;
            var consecutiveDecideWithoutExec = 0; // Track how many times DecideActionAsync is called without executing a tool

            // Step 4-7: Generate params -> review -> execute -> validate, loop up to hard limit
            while (decision.ShouldCallTool && decision.ToolName != null && history.Calls.Count < maxToolCalls && consecutiveDecideWithoutExec < MaxConsecutiveDecideWithoutExecution)
            {
                reasoningSteps.Add($"[PLAN] Call tool '{decision.ToolName}'");
                _logger.LogInformation(
                    "[AI-TOOL-DECISION] tool={Tool} params={Params}",
                    decision.ToolName, decision.ToolParameters?.ToString() ?? "{}");

                // Kiểm tra tool có được phép sử dụng trong context này không
                var allowedTools = _toolRegistry.GetAllowedTools(context);
                var isToolAllowed = allowedTools.Any(t => t.Name.Equals(decision.ToolName, StringComparison.OrdinalIgnoreCase));

                if (!isToolAllowed)
                {
                    reasoningSteps.Add($"Tool '{decision.ToolName}' not allowed for this context");
                    consecutiveDecideWithoutExec++;
                    decision = await DecideActionAsync(
                        userQuestion,
                        systemPrompt,
                        toolsManifest,
                        history,
                        context,
                        cancellationToken,
                        isContinuation: true,
                        consecutiveDecideWithoutExecution: consecutiveDecideWithoutExec);
                    continue; // LLM được chọn tool khác
                }

                // Step 4: Parameter generation + review
                var reviewed = await ReviewPlannedToolAsync(userQuestion, context, decision, cancellationToken);
                decision.ToolParameters = reviewed.ToolParameters;
                _logger.LogInformation(
                    "[AI-PARAM-REVIEW] state={State} tool={Tool} accepted={Accepted} note={Note} params={Params}",
                    reviewed.ReviewState,
                    decision.ToolName,
                    reviewed.IsAccepted,
                    reviewed.ReviewNote ?? "",
                    reviewed.ToolParameters?.ToString() ?? "{}");
                if (!string.IsNullOrWhiteSpace(reviewed.ReviewNote))
                {
                    reasoningSteps.Add($"[REVIEW] {reviewed.ReviewNote}");
                }

                if (!reviewed.IsAccepted)
                {
                    parameterReviewFailures++;
                    if (!string.IsNullOrWhiteSpace(reviewed.SuggestedToolName))
                    {
                        reasoningSteps.Add($"[REVIEW] Suggested tool: {reviewed.SuggestedToolName}");
                        
                        // If reviewer suggests a specific tool, try that tool instead of replanning
                        decision = new AgentDecision
                        {
                            ShouldCallTool = true,
                            ToolName = reviewed.SuggestedToolName,
                            ToolParameters = reviewed.SuggestedParameters
                        };
                        reasoningSteps.Add($"[REVIEW] Switching to suggested tool: {reviewed.SuggestedToolName}");
                        continue;
                    }

                    if (parameterReviewFailures >= 2)
                    {
                        reasoningSteps.Add("[REVIEW] Parameter review failed repeatedly, proceeding with current parameters.");
                        reviewed = reviewed with { IsAccepted = true };
                    }
                    else
                    {
                        consecutiveDecideWithoutExec++;
                        decision = await PlanNextActionAsync(
                            userQuestion,
                            systemPrompt,
                            toolsManifest,
                            history,
                            context,
                            cancellationToken,
                            isContinuation: true,
                            consecutiveDecideWithoutExecution: consecutiveDecideWithoutExec);
                        continue;
                    }
                }
                else
                {
                    parameterReviewFailures = 0;
                }

                var fitVerdict = ReviewToolFit(intent, decision);
                if (!fitVerdict.IsAccepted)
                {
                    reasoningSteps.Add($"[REVIEW] Tool fit rejected: {fitVerdict.ReviewNote}");
                    if (!string.IsNullOrWhiteSpace(fitVerdict.SuggestedToolName))
                    {
                        reasoningSteps.Add($"[REVIEW] Suggested tool: {fitVerdict.SuggestedToolName}");
                    }

                    consecutiveDecideWithoutExec++;
                    decision = await PlanNextActionAsync(
                        userQuestion,
                        systemPrompt,
                        toolsManifest,
                        history,
                        context,
                        cancellationToken,
                        isContinuation: true,
                        consecutiveDecideWithoutExecution: consecutiveDecideWithoutExec);
                    continue;
                }

                // Guard: mot tool chi duoc phep thanh cong 1 lan trong cung 1 turn.
                // Neu tool da thanh cong truoc do voi data hop le, dung du lieu cu de tra loi thay vi goi lai.
                var previousSuccessfulCall = history.Calls
                    .LastOrDefault(c => c.ToolName.Equals(decision.ToolName, StringComparison.OrdinalIgnoreCase) && c.Result.IsSuccess);
                if (previousSuccessfulCall != null)
                {
                    var forcedAnswer = BuildForcedAnswerFromToolResult(previousSuccessfulCall.ToolName, previousSuccessfulCall.Result.Data);
                    if (forcedAnswer != null)
                    {
                        // Data is valid — skip tool call, synthesize from cached result
                        reasoningSteps.Add($"[GUARD] Tool '{decision.ToolName}' already succeeded with valid data. Skip repeated call and finalize.");
                        decision = new AgentDecision
                        {
                            ShouldCallTool = false,
                            FinalAnswer = forcedAnswer
                        };
                        break;
                    }
                    // Data is null/empty — let tool execute again
                }

                // Step 5: Tool execution
                _logger.LogInformation("[TOOL-CALL] Executing tool={ToolName}", decision.ToolName);
                var toolResult = await ExecuteToolAsync(decision.ToolName, decision.ToolParameters!, context, cancellationToken);
                history.AddCall(decision.ToolName, decision.ToolParameters!, toolResult);
                consecutiveDecideWithoutExec = 0; // Tool executed, reset counter
                await SaveTaskPaginationStateIfNeededAsync(context, decision.ToolName, decision.ToolParameters!, toolResult);

                // Step 6: Result validation
                if (toolResult.IsSuccess)
                {
                    if (!ValidateToolResult(decision.ToolName, toolResult, out var validationNote))
                    {
                        reasoningSteps.Add($"[VALIDATE] {decision.ToolName}: {validationNote}");
                        decision = await PlanNextActionAsync(
                            userQuestion,
                            systemPrompt,
                            toolsManifest,
                            history,
                            context,
                            cancellationToken,
                            isContinuation: true);
                        continue;
                    }

                    _logger.LogInformation(
                        "[AI-TOOL-OK] tool={Tool} success=true elapsedMs={Ms} data={Summary}",
                        decision.ToolName, toolResult.ExecutionTimeMs, toolResult.GetDataSummary());

                    // Use generic formatter for all tools
                    var forcedAnswer = BuildForcedAnswerFromToolResult(decision.ToolName, toolResult.Data)
                        ?? "Da lay du lieu thanh cong.";

                    reasoningSteps.Add($"[GUARD] {decision.ToolName} succeeded -> finalize and stop the tool loop.");
                    decision = new AgentDecision
                    {
                        ShouldCallTool = false,
                        FinalAnswer = forcedAnswer
                    };
                    break;
                }
                else
                {
                    _logger.LogWarning(
                        "[AI-TOOL-FAIL] tool={Tool} success=false error={Error}",
                        decision.ToolName, toolResult.ErrorMessage ?? "unknown");
                }

                if (!toolResult.IsSuccess)
                {
                    reasoningSteps.Add($"Tool '{decision.ToolName}' failed: {toolResult.ErrorMessage}");

                    // Khi search_documents that bai >= 2 lan voi query rong -> chuyen sang get_group_documents
                    if (decision.ToolName == "search_documents" &&
                        HasEmptyQuery(decision.ToolParameters!) &&
                        history.Calls.Count(c => c.ToolName == "search_documents" && HasEmptyQuery(c.Parameters)) >= 2)
                    {
                        _logger.LogWarning(
                            "[AI-DOCS-FIRST] search_documents failed {Count} times with empty query. "
                            + "Redirecting to get_group_documents to get document list first.",
                            history.Calls.Count(c => c.ToolName == "search_documents"));

                        reasoningSteps.Add("Redirecting to get_group_documents - LLM does not know available documents");
                        decision = new AgentDecision
                        {
                            ShouldCallTool = true,
                            ToolName = "get_group_documents",
                            ToolParameters = new JsonObject()
                        };
                        continue;
                    }

                    // Step 7: Feed validation/error info back to planning loop
                    consecutiveDecideWithoutExec++;
                    decision = await DecideActionAsync(
                        userQuestion,
                        systemPrompt,
                        toolsManifest,
                        history,
                        context,
                        cancellationToken,
                        isContinuation: true,
                        consecutiveDecideWithoutExecution: consecutiveDecideWithoutExec);
                    continue; // LLM được retry — KHÔNG break
                }
            }

            // Step 8: Final response generation
            sw.Stop();

            // Log tổng kết tool calls
            var toolSummary = string.Join(", ", history.Calls.Select(c =>
                $"{c.ToolName}:{(c.Result.IsSuccess ? "OK" : "FAIL")}"));
            _logger.LogInformation(
                "[AI-TOOL-SUMMARY] toolsCalled={Count} tools={Summary} maxReached={Max}",
                history.Calls.Count, toolSummary, history.Calls.Count >= _config.MaxToolCalls);

            // Xác định FallbackReason
            string? fallbackReason = null;
            string? finalAnswer = null;

            if (!string.IsNullOrWhiteSpace(decision?.FinalAnswer))
            {
                var synthesisSystemPrompt = BuildMarkdownSynthesisSystemPrompt(context.Language);
                finalAnswer = await _llmService.GenerateTextResponseAsync(
                    synthesisSystemPrompt,
                    decision.FinalAnswer,
                    string.Empty,
                    cancellationToken);

                if (string.IsNullOrWhiteSpace(finalAnswer))
                {
                    finalAnswer = decision.FinalAnswer;
                }

                _logger.LogInformation(
                    "[AI-SUCCESS] ToolCalls={Count} AnswerLen={Len} Question={Q}",
                    history.Calls.Count,
                    finalAnswer.Length,
                    userQuestion);
            }
            else if (history.Calls.Count >= _config.MaxToolCalls)
            {
                fallbackReason = $"MaxToolCalls reached ({maxToolCalls}). LLM made no final_answer after {history.Calls.Count} tool calls.";
            }
            else if (history.Calls.Count > 0)
            {
                var last = history.Calls.Last();
                fallbackReason = last.Result.IsSuccess
                    ? $"All {history.Calls.Count} tool(s) succeeded but LLM returned empty final_answer."
                    : $"Tool '{last.ToolName}' failed: {last.Result.ErrorMessage ?? "unknown"}. LLM could not recover.";
            }
            else
            {
                fallbackReason = "LLM returned empty final_answer on first call (no tools attempted).";
            }

            // Log chi tiết khi fallback fire
            if (fallbackReason != null)
            {
                _logger.LogWarning(
                    "[AI-FALLBACK] Reason={Reason} ToolCalls={Count} Question={Q}",
                    fallbackReason,
                    history.Calls.Count,
                    userQuestion);

                foreach (var call in history.Calls)
                {
                    _logger.LogInformation(
                        "[AI-FALLBACK-TOOL] Tool={Tool} Success={Succ} Error={Err} TimeMs={Ms}",
                        call.ToolName,
                        call.Result.IsSuccess,
                        call.Result.ErrorMessage ?? "",
                        call.Result.ExecutionTimeMs);
                }
            }

            var result = new AIAgentResult
            {
                Answer = !string.IsNullOrWhiteSpace(finalAnswer)
                    ? finalAnswer
                    : "Xin lỗi, AI không trả lời được câu hỏi này.",
                ReasoningSteps = reasoningSteps,
                ToolCalls = history.Calls,
                ProcessingTimeMs = sw.ElapsedMilliseconds,
                ToolCallCount = history.Calls.Count,
                Success = true,
                FallbackReason = fallbackReason,
                // Token usage is tracked across all LLM calls in ProcessAsync
                TokenUsage = _currentTokenUsage
            };

            _logger.LogInformation(
                "AIAgent completed: Question={Question}, ToolsCalled={Count}, ToolNames={Tools}, Time={Ms}ms",
                userQuestion.Length > 50 ? userQuestion[..50] + "..." : userQuestion,
                history.Calls.Count,
                string.Join(",", history.Calls.Select(c => c.ToolName)),
                sw.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "AIAgent error");

            return new AIAgentResult
            {
                Answer = "Xin lỗi, đã xảy ra lỗi khi xử lý câu hỏi của bạn.",
                ReasoningSteps = reasoningSteps,
                ProcessingTimeMs = sw.ElapsedMilliseconds,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Process question with streaming final answer.
    /// Runs ReAct loop synchronously (tool calls), but streams LLM response.
    /// For SSE streaming endpoint.
    /// </summary>
    public async IAsyncEnumerable<AIStreamChunk> ProcessStreamAsync(
        string userQuestion,
        AIQueryContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        AIStreamResult? result = null;
        Exception? caughtException = null;

        try
        {
            result = await ProcessStreamInternalAsync(userQuestion, context, cancellationToken);
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        if (caughtException != null)
        {
            _logger.LogError(caughtException, "AIAgent ProcessStreamAsync error");
            // Cannot yield in catch, so yield directly from if block (after catch)
            yield return new AIStreamChunk
            {
                Type = "error",
                ErrorMessage = "Đã xảy ra lỗi khi xử lý yêu cầu. Vui lòng thử lại sau."
            };
            yield break;
        }

        if (result == null)
        {
            _logger.LogError("AIAgent ProcessStreamAsync returned null result without exception");
            yield return new AIStreamChunk
            {
                Type = "error",
                ErrorMessage = "Đã xảy ra lỗi khi xử lý yêu cầu. Vui lòng thử lại sau."
            };
            yield break;
        }

        // Yield metadata first
        yield return new AIStreamChunk
        {
            Type = "metadata",
            RemainingRequests = null,
            DailyLimit = null,
            ToolCount = result.ToolCount,
            ProcessingTimeMs = result.ProcessingTimeMs,
            InputTokens = result.TokenUsage?.InputTokens,
            OutputTokens = result.TokenUsage?.OutputTokens,
            CachedTokens = result.TokenUsage?.CachedTokens,
            ThinkingTokens = result.TokenUsage?.ThinkingTokens
        };

        // Yield chunks
        foreach (var chunk in result.Chunks)
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// Internal processing that returns a result instead of yielding directly
    /// </summary>
    private async Task<AIStreamResult> ProcessStreamInternalAsync(
        string userQuestion,
        AIQueryContext context,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var history = new ToolExecutionHistory();
        var reasoningSteps = new List<string>();
        var chunks = new List<AIStreamChunk>();

        // Reset token usage tracking for this streaming request
        _currentTokenUsage = null;

        // Step 1: Intent understanding
        var intent = AnalyzeIntent(userQuestion, context);
        reasoningSteps.Add($"[INTENT] {intent.Summary}");

        var paginationState = await GetTaskPaginationStateAsync(context);
        var isTaskFollowup = IsTaskPaginationFollowup(userQuestion);

        if (isTaskFollowup)
        {
            reasoningSteps.Add("[FOLLOWUP] Detected task pagination follow-up. Use cached task state.");
            await TryExecuteTaskFollowupAsync(userQuestion, context, history, reasoningSteps, paginationState, cancellationToken);
        }

        if (intent.IsDocumentIntent && !intent.IsTaskIntent)
        {
            await AutoFetchDocumentContextAsync(userQuestion, history, reasoningSteps, context, cancellationToken);
        }

        reasoningSteps.Add($"[ANALYZE] Question={userQuestion}");

        // Step 2: Tool planning
        var toolsManifest = _toolRegistry.GetToolsManifestForContext(context);
        var systemPrompt = GetRoleSystemPrompt(context);

        var decision = await PlanNextActionAsync(
            userQuestion,
            systemPrompt,
            toolsManifest,
            history,
            context,
            cancellationToken);

        var maxToolCalls = GetEffectiveMaxToolCalls();
        var parameterReviewFailures = 0;
        var consecutiveDecideWithoutExec = 0;

        // Step 3-7: Tool loop (same as ProcessAsync)
        while (decision.ShouldCallTool
               && decision.ToolName != null
               && history.Calls.Count < maxToolCalls
               && consecutiveDecideWithoutExec < MaxConsecutiveDecideWithoutExecution)
        {
            reasoningSteps.Add($"[PLAN] Call tool '{decision.ToolName}'");

            var allowedTools = _toolRegistry.GetAllowedTools(context);
            var isToolAllowed = allowedTools.Any(t => t.Name.Equals(decision.ToolName, StringComparison.OrdinalIgnoreCase));

            if (!isToolAllowed)
            {
                reasoningSteps.Add($"Tool '{decision.ToolName}' not allowed for this context");
                consecutiveDecideWithoutExec++;
                decision = await DecideActionAsync(
                    userQuestion,
                    systemPrompt,
                    toolsManifest,
                    history,
                    context,
                    cancellationToken,
                    isContinuation: true,
                    consecutiveDecideWithoutExecution: consecutiveDecideWithoutExec);
                continue;
            }

            var reviewed = await ReviewPlannedToolAsync(userQuestion, context, decision, cancellationToken);
            decision.ToolParameters = reviewed.ToolParameters;

            if (!reviewed.IsAccepted)
            {
                parameterReviewFailures++;
                if (!string.IsNullOrWhiteSpace(reviewed.SuggestedToolName))
                {
                    decision = new AgentDecision
                    {
                        ShouldCallTool = true,
                        ToolName = reviewed.SuggestedToolName,
                        ToolParameters = reviewed.ToolParameters
                    };
                    continue;
                }

                if (parameterReviewFailures >= 2)
                {
                    reviewed = reviewed with { IsAccepted = true };
                }
                else
                {
                    consecutiveDecideWithoutExec++;
                    decision = await PlanNextActionAsync(
                        userQuestion,
                        systemPrompt,
                        toolsManifest,
                        history,
                        context,
                        cancellationToken,
                        isContinuation: true,
                        consecutiveDecideWithoutExecution: consecutiveDecideWithoutExec);
                    continue;
                }
            }
            else
            {
                parameterReviewFailures = 0;
            }

            var fitVerdict = ReviewToolFit(intent, decision);
            if (!fitVerdict.IsAccepted)
            {
                reasoningSteps.Add($"[REVIEW] Tool fit rejected: {fitVerdict.ReviewNote}");
                consecutiveDecideWithoutExec++;
                decision = await PlanNextActionAsync(
                    userQuestion,
                    systemPrompt,
                    toolsManifest,
                    history,
                    context,
                    cancellationToken,
                    isContinuation: true,
                    consecutiveDecideWithoutExecution: consecutiveDecideWithoutExec);
                continue;
            }

            var previousSuccessfulCall = history.Calls
                .LastOrDefault(c => c.ToolName.Equals(decision.ToolName, StringComparison.OrdinalIgnoreCase) && c.Result.IsSuccess);
            if (previousSuccessfulCall != null)
            {
                var forcedAnswer = BuildForcedAnswerFromToolResult(previousSuccessfulCall.ToolName, previousSuccessfulCall.Result.Data);
                if (forcedAnswer != null)
                {
                    reasoningSteps.Add($"[GUARD] Tool '{decision.ToolName}' already succeeded with valid data. Skip repeated call.");
                    decision = new AgentDecision
                    {
                        ShouldCallTool = false,
                        FinalAnswer = forcedAnswer
                    };
                    break;
                }

                // Tool succeeded but returned null data — count how many times it was called
                var sameToolCalls = history.Calls
                    .Count(c => c.ToolName.Equals(decision.ToolName, StringComparison.OrdinalIgnoreCase) && c.Result.IsSuccess);
                _logger.LogWarning(
                    "[GUARD-NULL-DATA] Tool={Tool} Data=null, SameToolCalls={Count}",
                    decision.ToolName, sameToolCalls);
                if (sameToolCalls >= 2)
                {
                    // Stop calling this tool — data is consistently null
                    var fallback = $"Da goi tool '{decision.ToolName}' {sameToolCalls} lan nhung khong co du lieu. Vui long dua ra cau tra loi dua tren du lieu hien co.";
                    reasoningSteps.Add($"[GUARD] Tool '{decision.ToolName}' called {sameToolCalls} times with null data. Stopping.");
                    decision = new AgentDecision
                    {
                        ShouldCallTool = false,
                        FinalAnswer = fallback
                    };
                    break;
                }
            }

            // Tool execution
            var toolResult = await ExecuteToolAsync(decision.ToolName, decision.ToolParameters!, context, cancellationToken);
            history.AddCall(decision.ToolName, decision.ToolParameters!, toolResult);
            consecutiveDecideWithoutExec = 0;
            await SaveTaskPaginationStateIfNeededAsync(context, decision.ToolName, decision.ToolParameters!, toolResult);

            if (toolResult.IsSuccess)
            {
                if (!ValidateToolResult(decision.ToolName, toolResult, out var validationNote))
                {
                    reasoningSteps.Add($"[VALIDATE] {decision.ToolName}: {validationNote}");
                    consecutiveDecideWithoutExec++;
                    decision = await PlanNextActionAsync(
                        userQuestion,
                        systemPrompt,
                        toolsManifest,
                        history,
                        context,
                        cancellationToken,
                        isContinuation: true,
                        consecutiveDecideWithoutExecution: consecutiveDecideWithoutExec);
                    continue;
                }

                // Use generic formatter for all tools
                var forcedAnswer = BuildForcedAnswerFromToolResult(decision.ToolName, toolResult.Data)
                    ?? "Da lay du lieu thanh cong.";

                reasoningSteps.Add($"[GUARD] {decision.ToolName} succeeded -> finalize.");
                decision = new AgentDecision
                {
                    ShouldCallTool = false,
                    FinalAnswer = forcedAnswer
                };
                break;
            }
            else
            {
                reasoningSteps.Add($"Tool '{decision.ToolName}' failed: {toolResult.ErrorMessage}");

                if (decision.ToolName == "search_documents" &&
                    HasEmptyQuery(decision.ToolParameters!) &&
                    history.Calls.Count(c => c.ToolName == "search_documents" && HasEmptyQuery(c.Parameters)) >= 2)
                {
                    decision = new AgentDecision
                    {
                        ShouldCallTool = true,
                        ToolName = "get_group_documents",
                        ToolParameters = new JsonObject()
                    };
                    continue;
                }

                decision = await DecideActionAsync(
                    userQuestion,
                    systemPrompt,
                    toolsManifest,
                    history,
                    context,
                    cancellationToken,
                    isContinuation: true,
                    consecutiveDecideWithoutExecution: ++consecutiveDecideWithoutExec);
            }
        }

        sw.Stop();

        // Check if we have a forced answer (from tool results)
        if (!string.IsNullOrWhiteSpace(decision?.FinalAnswer))
        {
            _logger.LogInformation("[AI-SYNTHESIS] Calling LLM to synthesize final answer from tool results");
            reasoningSteps.Add("[SYNTHESIS] Final answer from tool results - calling LLM to format response and suggest next steps");

            var synthesisSystemPrompt = BuildMarkdownSynthesisSystemPrompt(context.Language);
            await foreach (var chunk in _llmService.GenerateAnswerStreamAsync(
                synthesisSystemPrompt,
                decision.FinalAnswer,
                string.Empty,
                cancellationToken,
                forceTextMode: true))
            {
                if (!string.IsNullOrEmpty(chunk))
                {
                    chunks.Add(new AIStreamChunk { Type = "chunk", Content = chunk });
                }
            }

            chunks.Add(new AIStreamChunk { Type = "done" });
            return new AIStreamResult
            {
                ToolCount = history.Calls.Count,
                ProcessingTimeMs = sw.ElapsedMilliseconds,
                Chunks = chunks,
                TokenUsage = _currentTokenUsage
            };
        }

        // Build prompt for streaming LLM call
        var prompt = BuildPromptForFinalAnswer(
            userQuestion,
            systemPrompt,
            toolsManifest,
            history,
            context,
            cancellationToken);

        _logger.LogInformation(
            "[LLM-STREAM] Starting streaming response. Question={Question}",
            userQuestion.Length > 50 ? userQuestion[..50] + "..." : userQuestion);

        // Stream LLM response chunks
        await foreach (var chunk in _llmService.GenerateAnswerStreamAsync(
            prompt,
            userQuestion,
            "",
            cancellationToken))
        {
            if (!string.IsNullOrEmpty(chunk))
            {
                chunks.Add(new AIStreamChunk { Type = "chunk", Content = chunk });
            }
        }

        chunks.Add(new AIStreamChunk { Type = "done" });

        return new AIStreamResult
        {
            ToolCount = history.Calls.Count,
            ProcessingTimeMs = sw.ElapsedMilliseconds,
            Chunks = chunks,
            TokenUsage = _currentTokenUsage
        };
    }

    /// <summary>
    /// Build prompt for final answer generation (without response schema for streaming)
    /// </summary>
    private string BuildPromptForFinalAnswer(
        string userQuestion,
        string systemPrompt,
        JsonObject toolsManifest,
        ToolExecutionHistory history,
        AIQueryContext context,
        CancellationToken cancellationToken)
    {
        var promptBuilder = new System.Text.StringBuilder();

        promptBuilder.AppendLine(systemPrompt);
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("=== AVAILABLE TOOLS ===");
        promptBuilder.AppendLine(toolsManifest["tools"]?.ToString() ?? "[]");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("=== USER QUESTION ===");
        promptBuilder.AppendLine(userQuestion);
        promptBuilder.AppendLine();

        if (history.Calls.Count > 0)
        {
            promptBuilder.AppendLine("=== TOOL RESULTS ===");
            var recentCalls = history.Calls.TakeLast(3).ToList();
            foreach (var call in recentCalls)
            {
                promptBuilder.AppendLine($"Tool: {call.ToolName}");
                promptBuilder.AppendLine($"Parameters: {call.Parameters}");
                promptBuilder.AppendLine($"Result: {call.Result.ToJson()}");
                promptBuilder.AppendLine();
            }
        }

        promptBuilder.AppendLine("=== INSTRUCTIONS ===");
        promptBuilder.AppendLine("- Based on the tool results above, provide a clear and helpful answer");
        promptBuilder.AppendLine("- Format your answer in Vietnamese with proper markdown if needed");
        promptBuilder.AppendLine("- If tool results are empty or insufficient, say so honestly");

        return promptBuilder.ToString();
    }

    private static string? BuildForcedAnswerFromToolResult(string toolName, JsonObject? data)
    {
        if (data == null)
        {
            return null;
        }

        // Let the LLM decide the final format and next action suggestions using tool data as context.
        return BuildPromptForLLMSynthesis(toolName, data);
    }

    /// <summary>
    /// Build context for LLM to synthesize its own answer
    /// AI will decide format (table, list, markdown, etc.)
    /// </summary>
    private static string BuildPromptForLLMSynthesis(string toolName, JsonObject data)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Tool '{toolName}' returned the following data:");
        sb.AppendLine();
        sb.AppendLine(data.ToJsonString());
        sb.AppendLine();
        sb.AppendLine("TASK: Use the data above to answer the user's request in Vietnamese.");
        sb.AppendLine();
        sb.AppendLine("RULES:");
        sb.AppendLine("- Choose the best response format yourself: paragraph, bullets, table, or a mix.");
        sb.AppendLine("- DO NOT return JSON.");
        sb.AppendLine("- Highlight important items such as overdue tasks, high priority work, missing deadlines, or risks.");
        sb.AppendLine("- If the data suggests what to do next, include a short 'Gợi ý tiếp theo' section with 1-3 concrete actions.");
        sb.AppendLine("- Keep the answer concise but useful. Prefer readable markdown.");
        return sb.ToString();
    }

    private static string BuildCompactToolResultForPrompt(AIQueryResult result)
    {
        var payload = new JsonObject
        {
            ["is_success"] = JsonValue.Create(result.IsSuccess),
            ["execution_time_ms"] = JsonValue.Create(result.ExecutionTimeMs)
        };

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            payload["error_message"] = JsonValue.Create(result.ErrorMessage);
        }

        if (result.Data != null)
        {
            var compactData = result.Data.DeepClone() as JsonObject ?? new JsonObject();
            CompactNodeForPrompt(compactData, 0);
            payload["data"] = compactData;
        }

        var compactJson = payload.ToJsonString();
        if (compactJson.Length > MaxToolResultCharsForPrompt)
        {
            compactJson = compactJson[..MaxToolResultCharsForPrompt] + "... [truncated]";
        }

        return compactJson;
    }

    private static void CompactNodeForPrompt(JsonNode? node, int depth)
    {
        if (node == null || depth > 4)
        {
            return;
        }

        if (node is JsonObject obj)
        {
            var keys = obj.Select(kv => kv.Key).ToList();
            foreach (var key in keys)
            {
                var child = obj[key];
                if (child is JsonValue value)
                {
                    if (value.TryGetValue<string>(out var str) && str.Length > MaxStringCharsForPrompt)
                    {
                        obj[key] = JsonValue.Create(str[..MaxStringCharsForPrompt] + "...");
                    }
                    continue;
                }

                if (child is JsonArray arr)
                {
                    var removedCount = arr.Count - MaxArrayItemsForPrompt;
                    while (arr.Count > MaxArrayItemsForPrompt)
                    {
                        arr.RemoveAt(arr.Count - 1);
                    }

                    foreach (var item in arr)
                    {
                        CompactNodeForPrompt(item, depth + 1);
                    }

                    if (removedCount > 0)
                    {
                        obj[key + "_truncated_count"] = JsonValue.Create(removedCount);
                    }
                }
                else
                {
                    CompactNodeForPrompt(child, depth + 1);
                }
            }

            return;
        }

        if (node is JsonArray array)
        {
            while (array.Count > MaxArrayItemsForPrompt)
            {
                array.RemoveAt(array.Count - 1);
            }

            foreach (var item in array)
            {
                CompactNodeForPrompt(item, depth + 1);
            }
        }
    }

    private static string BuildMarkdownSynthesisSystemPrompt(string language)
    {
        var isEnglish = language.Equals("en", StringComparison.OrdinalIgnoreCase);

        return isEnglish
            ? "You are an AI assistant that turns tool data into a clean Markdown answer. Never return JSON. Use headings, bullet lists, and tables when useful. Include a short \"Next steps\" section when the data implies concrete actions."
            : "Ban la tro ly AI bien du lieu tu tool thanh cau tra loi Markdown sach se. Tuyet doi khong tra ve JSON. Hay dung tieu de, danh sach bullet va bang khi can. Neu du lieu goi y hanh dong cu the, hay them muc \"Goi y tiep theo\" ngan gon.";
    }

    /// <summary>
    /// Generic formatter - convert tool result JSON thành readable markdown
    /// Khong can handler riêng cho từng tool
    /// </summary>
    private static string FormatToolResultAsMarkdown(string toolName, JsonObject data)
    {
        var lines = new List<string>();
        var toolTitle = GetToolDisplayName(toolName);

        JsonArray? personalTasksArray = null;
        if (data.TryGetPropertyValue("personal_tasks", out var personalTasksNode) && personalTasksNode is JsonArray personalTasksJsonArray)
        {
            personalTasksArray = personalTasksJsonArray;
        }

        JsonArray? groupTasksArray = null;
        if (data.TryGetPropertyValue("group_tasks", out var groupTasksNode) && groupTasksNode is JsonArray groupTasksJsonArray)
        {
            groupTasksArray = groupTasksJsonArray;
        }

        if (personalTasksArray != null || groupTasksArray != null)
        {
            return FormatPersonalAndGroupTasksAsMarkdown(toolTitle, data, personalTasksArray, groupTasksArray);
        }

        // 1. Handle arrays (tasks, documents, members, etc.)
        JsonArray? itemArray = null;
        if (TryGetArray(data, "tasks", out var tasks)) { itemArray = tasks; }
        else if (TryGetArray(data, "documents", out var docs)) { itemArray = docs; }
        else if (TryGetArray(data, "members", out var members)) { itemArray = members; }
        else if (TryGetArray(data, "deadlines", out var deadlines)) { itemArray = deadlines; }
        else if (TryGetArray(data, "groups", out var groups)) { itemArray = groups; }
        else if (TryGetArray(data, "results", out var results)) { itemArray = results; }

        if (itemArray != null)
        {
            var count = itemArray.Count;

            // Summary count
            if (data.TryGetPropertyValue("total", out var totalNode) && totalNode != null)
            {
                lines.Add($"**{toolTitle}** - Tìm thấy {totalNode} kết quả:");
            }
            else if (data.TryGetPropertyValue("total_count", out var totalCountNode) && totalCountNode != null)
            {
                lines.Add($"**{toolTitle}** - Tổng cộng {totalCountNode} mục:");
            }
            else
            {
                lines.Add($"**{toolTitle}** - {count} kết quả:");
            }

            // Limit notice
            if (data.TryGetPropertyValue("summary", out var summaryNode) && summaryNode != null)
            {
                lines.Add($"_{summaryNode.GetValue<string>()}_");
            }

            // Format each item
            foreach (var item in itemArray.Take(10)) // Max 10 items displayed
            {
                if (item is not JsonObject obj) continue;
                lines.Add(FormatItemAsMarkdown(obj));
            }

            if (itemArray.Count > 10)
            {
                lines.Add($"... và {itemArray.Count - 10} mục khác.");
            }

            return string.Join("\n", lines);
        }

        // 2. Handle stats/analytics objects
        if (data.TryGetPropertyValue("task_statistics", out var statsNode) && statsNode is JsonObject stats)
        {
            lines.Add($"**{toolTitle}**");
            lines.Add(FormatStatsAsMarkdown(stats));

            // Group info if present
            if (data.TryGetPropertyValue("group_info", out var groupInfoNode) && groupInfoNode is JsonObject groupInfo)
            {
                var groupName = groupInfo.TryGetPropertyValue("name", out var nameNode) && nameNode != null
                    ? nameNode.GetValue<string>() : "Nhóm hiện tại";
                lines.Insert(0, $"## {groupName}");
            }

            return string.Join("\n", lines);
        }

        if (data.TryGetPropertyValue("statistics", out var statisticsNode) && statisticsNode is JsonObject statistics)
        {
            lines.Add($"**{toolTitle}**");
            lines.Add(FormatStatsAsMarkdown(statistics));
            return string.Join("\n", lines);
        }

        // 3. Handle risk analysis
        if (data.TryGetPropertyValue("risk_level", out var riskNode) && riskNode != null)
        {
            lines.Add($"**{toolTitle}**");
            var riskLevel = riskNode.GetValue<string>();
            var riskIcon = riskLevel switch
            {
                "HIGH" => "🔴",
                "MEDIUM" => "🟡",
                "LOW" => "🟢",
                _ => "⚪"
            };
            lines.Add($"{riskIcon} Mức độ rủi ro: **{riskLevel}**");

            if (data.TryGetPropertyValue("risk_factors", out var factorsNode) && factorsNode is JsonArray factors)
            {
                lines.Add("**Yếu tố rủi ro:**");
                foreach (var factor in factors.Take(5))
                {
                    if (factor is JsonObject factorObj)
                    {
                        var desc = factorObj.TryGetPropertyValue("description", out var descNode) && descNode != null
                            ? descNode.GetValue<string>() : factor.ToString();
                        lines.Add($"- {desc}");
                    }
                }
            }

            return string.Join("\n", lines);
        }

        // 4. Handle single object with common fields
        var commonTitle = data.TryGetPropertyValue("title", out var titleNode) && titleNode != null
            ? titleNode.GetValue<string>()
            : data.TryGetPropertyValue("name", out var nameObjNode) && nameObjNode != null
                ? nameObjNode.GetValue<string>()
                : toolTitle;

        lines.Add($"**{commonTitle}**");

        // Common fields
        var commonFields = new[] { "description", "status", "progress", "priority", "severity", "completion_rate", "member_count" };
        foreach (var field in commonFields)
        {
            if (data.TryGetPropertyValue(field, out var fieldNode) && fieldNode != null && fieldNode.GetValueKind() != JsonValueKind.Object && fieldNode.GetValueKind() != JsonValueKind.Array)
            {
                lines.Add($"- **{field.Replace("_", " ").ToUpperInvariant()}**: {fieldNode}");
            }
        }

        // 5. Fallback - just show data summary
        if (lines.Count <= 1)
        {
            return $"**{toolTitle}**: Đã lấy dữ liệu thành công. Xem chi tiết trong hệ thống.";
        }

        return string.Join("\n", lines);
    }

    private static string FormatPersonalAndGroupTasksAsMarkdown(
        string toolTitle,
        JsonObject data,
        JsonArray? personalTasks,
        JsonArray? groupTasks)
    {
        var lines = new List<string> { $"**{toolTitle}**" };

        if (data.TryGetPropertyValue("summary", out var summaryNode) && summaryNode != null)
        {
            lines.Add($"_{summaryNode.GetValue<string>()}_");
        }

        if (data.TryGetPropertyValue("personal_count", out var personalCountNode) && personalCountNode != null)
        {
            var personalCount = personalCountNode.GetValue<int>();
            var groupCount = data.TryGetPropertyValue("group_count", out var groupCountNode) && groupCountNode != null
                ? groupCountNode.GetValue<int>()
                : groupTasks?.Count ?? 0;
            lines.Add($"- **Tổng quan**: {personalCount} công việc cá nhân, {groupCount} công việc nhóm.");
        }

        if (personalTasks != null)
        {
            lines.Add("");
            lines.Add("### Công việc cá nhân");
            if (personalTasks.Count == 0)
            {
                lines.Add("- Không có công việc cá nhân.");
            }
            else
            {
                foreach (var item in personalTasks.Take(10))
                {
                    if (item is JsonObject obj)
                    {
                        lines.Add(FormatItemAsMarkdown(obj));
                    }
                }

                if (personalTasks.Count > 10)
                {
                    lines.Add($"... và {personalTasks.Count - 10} công việc cá nhân khác.");
                }
            }
        }

        if (groupTasks != null)
        {
            lines.Add("");
            lines.Add("### Công việc nhóm");
            if (groupTasks.Count == 0)
            {
                lines.Add("- Không có công việc nhóm.");
            }
            else
            {
                foreach (var item in groupTasks.Take(10))
                {
                    if (item is JsonObject obj)
                    {
                        lines.Add(FormatItemAsMarkdown(obj));
                    }
                }

                if (groupTasks.Count > 10)
                {
                    lines.Add($"... và {groupTasks.Count - 10} công việc nhóm khác.");
                }
            }
        }

        if (data.TryGetPropertyValue("recommendation", out var recommendationNode) && recommendationNode != null)
        {
            lines.Add("");
            lines.Add("**Khuyến nghị**");
            lines.Add(recommendationNode.ToString());
        }

        return string.Join("\n", lines.Where(line => line != null));
    }

    private static bool TryGetArray(JsonObject data, string key, out JsonArray? array)
    {
        array = null;
        if (data.TryGetPropertyValue(key, out var node) && node is JsonArray arr && arr.Count > 0)
        {
            array = arr;
            return true;
        }
        return false;
    }

    private static string FormatItemAsMarkdown(JsonObject item)
    {
        var title = item.TryGetPropertyValue("title", out var t) && t != null ? t.GetValue<string>() : "N/A";
        var status = item.TryGetPropertyValue("status", out var s) && s != null ? s.GetValue<string>() : null;
        var priority = item.TryGetPropertyValue("priority", out var p) && p != null ? p.GetValue<string>() : null;
        var dueDate = item.TryGetPropertyValue("due_date", out var d) && d != null ? d.GetValue<string>() : null;
        var progress = item.TryGetPropertyValue("progress", out var pg) && pg != null ? $"{pg}%" : null;
        var groupName = item.TryGetPropertyValue("group_name", out var g) && g != null ? $"[{g.GetValue<string>()}]" : null;
        var source = item.TryGetPropertyValue("source", out var src) && src != null ? $"({src.GetValue<string>()})" : null;

        var parts = new List<string> { $"- {title}" };
        if (groupName != null) parts.Add(groupName);
        if (status != null) parts.Add($"| Status: {status}");
        if (priority != null) parts.Add($"| Priority: {priority}");
        if (progress != null) parts.Add($"| Progress: {progress}");
        if (dueDate != null && dueDate != "") parts.Add($"| Due: {dueDate}");
        if (source != null) parts.Add(source);

        return string.Join(" ", parts);
    }

    private static string FormatStatsAsMarkdown(JsonObject stats)
    {
        var lines = new List<string>();
        var mappings = new Dictionary<string, string>
        {
            ["total_tasks"] = "Tổng task",
            ["completed_tasks"] = "Đã hoàn thành",
            ["in_progress_tasks"] = "Đang thực hiện",
            ["not_started_tasks"] = "Chưa bắt đầu",
            ["overdue_tasks"] = "Quá hạn",
            ["completion_percentage"] = "Tỷ lệ hoàn thành",
            ["active_groups"] = "Nhóm đang hoạt động",
            ["total_members"] = "Tổng thành viên"
        };

        foreach (var (key, label) in mappings)
        {
            if (stats.TryGetPropertyValue(key, out var node) && node != null)
            {
                var value = node.GetValueKind() == JsonValueKind.Number
                    ? node.GetValue<int>().ToString()
                    : node.ToString();
                lines.Add($"- **{label}**: {value}");
            }
        }

        return lines.Count > 0 ? string.Join("\n", lines) : "- Không có dữ liệu thống kê.";
    }

    private static string GetToolDisplayName(string toolName)
    {
        var displayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["get_tasks"] = "Danh sách công việc",
            ["get_personal_tasks"] = "Công việc cá nhân",
            ["get_group_stats"] = "Thống kê nhóm",
            ["get_personal_stats"] = "Thống kê cá nhân",
            ["get_studio_analytics"] = "Analytics Studio",
            ["get_members"] = "Thành viên nhóm",
            ["get_deadlines"] = "Deadline",
            ["get_personal_deadlines"] = "Deadline cá nhân",
            ["search_documents"] = "Tài liệu tìm kiếm",
            ["search_studio_documents"] = "Tài liệu Studio",
            ["get_group_documents"] = "Tài liệu nhóm",
            ["get_studio_groups"] = "Danh sách nhóm",
            ["get_group_risk"] = "Phân tích rủi ro nhóm",
            ["get_risk_groups"] = "Nhóm rủi ro",
            ["get_studio_health"] = "Tình trạng Studio",
            ["compare_groups"] = "So sánh nhóm",
            ["get_group_performance"] = "Hiệu suất nhóm",
            ["get_member_permissions"] = "Quyền thành viên"
        };

        return displayNames.TryGetValue(toolName, out var name) ? name : toolName.Replace("_", " ").ToUpperInvariant();
    }

    private static bool HasTaskFilterParameters(JsonObject? parameters)
    {
        if (parameters == null) return false;

        static bool HasValue(JsonNode? n) => n != null && !string.IsNullOrWhiteSpace(n.ToString());

        return HasValue(parameters["priority"])
               || HasValue(parameters["min_priority"])
               || HasValue(parameters["severity"])
               || HasValue(parameters["min_severity"])
               || HasValue(parameters["status"])
               || HasValue(parameters["status_category"]);
    }

    private static bool IsTaskPaginationFollowup(string question)
    {
        if (string.IsNullOrWhiteSpace(question)) return false;

        var q = question.Trim().ToLowerInvariant();
        return q.Contains("xem tiep")
               || q.Contains("xem tiếp")
               || q.Contains("trang tiep")
               || q.Contains("trang tiếp")
               || q.Contains("next page")
               || q == "next"
               || q == "more";
    }

    private string GetTaskPaginationSessionKey(AIQueryContext context)
    {
        if (!context.GroupId.HasValue)
        {
            return $"ai:task_pagination:{context.UserId}:nogroup:{context.SessionId ?? "default"}";
        }

        var session = string.IsNullOrWhiteSpace(context.SessionId)
            ? "default"
            : context.SessionId.Trim();

        return $"ai:task_pagination:{context.UserId}:{context.GroupId.Value}:{session}";
    }

    private async Task<AITaskPaginationSessionState?> GetTaskPaginationStateAsync(AIQueryContext context)
    {
        try
        {
            var key = GetTaskPaginationSessionKey(context);
            return await _cacheService.GetAsync<AITaskPaginationSessionState>(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FOLLOWUP] Failed to read task pagination session state");
            return null;
        }
    }

    private async Task SaveTaskPaginationStateIfNeededAsync(
        AIQueryContext context,
        string toolName,
        JsonObject parameters,
        AIQueryResult result)
    {
        if (!toolName.Equals("get_tasks", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!result.IsSuccess || result.Data == null)
        {
            return;
        }

        int page = 0;
        int pageSize = 20;
        int totalPages = 1;

        if (result.Data.TryGetPropertyValue("current_page", out var pNode) && pNode != null)
        {
            page = pNode.GetValue<int>();
        }
        if (result.Data.TryGetPropertyValue("page_size", out var psNode) && psNode != null)
        {
            pageSize = psNode.GetValue<int>();
        }
        if (result.Data.TryGetPropertyValue("total_pages", out var tpNode) && tpNode != null)
        {
            totalPages = tpNode.GetValue<int>();
        }

        if (page <= 0)
        {
            if (parameters.TryGetPropertyValue("page", out var reqPageNode) && reqPageNode != null)
            {
                page = reqPageNode.GetValue<int>();
            }
            if (page <= 0) page = 1;
        }

        if (pageSize <= 0)
        {
            if (parameters.TryGetPropertyValue("page_size", out var reqPageSizeNode) && reqPageSizeNode != null)
            {
                pageSize = reqPageSizeNode.GetValue<int>();
            }
            if (pageSize <= 0) pageSize = 20;
        }

        if (totalPages <= 0) totalPages = 1;

        var state = new AITaskPaginationSessionState
        {
            LastPage = page,
            LastPageSize = pageSize,
            LastTotalPages = totalPages,
            UpdatedAt = DateTime.UtcNow
        };

        try
        {
            var key = GetTaskPaginationSessionKey(context);
            await _cacheService.SetAsync(key, state, TimeSpan.FromMinutes(30));
            _logger.LogInformation(
                "[FOLLOWUP] Saved task pagination state: key={Key} page={Page}/{TotalPages} pageSize={PageSize}",
                key, state.LastPage, state.LastTotalPages, state.LastPageSize);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FOLLOWUP] Failed to save task pagination session state");
        }
    }

    private async Task TryExecuteTaskFollowupAsync(
        string userQuestion,
        AIQueryContext context,
        ToolExecutionHistory history,
        List<string> reasoningSteps,
        AITaskPaginationSessionState? state,
        CancellationToken cancellationToken)
    {
        if (state == null)
        {
            reasoningSteps.Add("[FOLLOWUP] No previous pagination state. Let LLM decide normally.");
            return;
        }

        var nextPage = state.LastPage + 1;
        if (nextPage > state.LastTotalPages)
        {
            nextPage = state.LastTotalPages;
        }

        var followupParams = new JsonObject
        {
            ["page"] = nextPage,
            ["page_size"] = state.LastPageSize > 0 ? state.LastPageSize : 20
        };

        _logger.LogInformation(
            "[FOLLOWUP] Auto-call get_tasks for follow-up question={Question} with page={Page} pageSize={PageSize}",
            userQuestion, nextPage, state.LastPageSize);

        var result = await ExecuteToolAsync("get_tasks", followupParams, context, cancellationToken);
        history.AddCall("get_tasks", followupParams, result);
        await SaveTaskPaginationStateIfNeededAsync(context, "get_tasks", followupParams, result);

        reasoningSteps.Add(result.IsSuccess
            ? $"[FOLLOWUP] Auto loaded get_tasks page={nextPage}, page_size={state.LastPageSize}"
            : $"[FOLLOWUP] Auto get_tasks failed: {result.ErrorMessage}");
    }

    /// <summary>
    /// [METHOD A] Auto-fetch document context cho MỌI query — luôn chạy trước get_group_documents + search_documents
    /// KHÔNG đếm vào MaxToolCalls budget của LLM
    /// 
    /// Flow:
    /// 1. Gọi get_group_documents để lấy danh sách tài liệu
    /// 2. Trích xuất tên tài liệu từ câu hỏi user (nếu có)
    /// 3. Match tên với danh sách để lấy document IDs
    /// 4. Gọi search_documents với:
    ///    - document_id: nếu user đề cập tài liệu cụ thể
    ///    - top_k: 5 (lấy top 5 chunks)
    ///    - query: câu hỏi của user
    /// </summary>
    private async Task AutoFetchDocumentContextAsync(
        string userQuestion,
        ToolExecutionHistory history,
        List<string> reasoningSteps,
        AIQueryContext context,
        CancellationToken cancellationToken)
    {
        reasoningSteps.Add("[METHOD-A] Auto-fetching document context...");

        // Auto-Step 1: get_group_documents — lấy danh sách file có sẵn trong group
        var docListResult = await ExecuteToolAsync(
            "get_group_documents",
            new JsonObject(),
            context,
            cancellationToken);
        history.AddCall("get_group_documents", new JsonObject(), docListResult);
        reasoningSteps.Add($"[METHOD-A] get_group_documents: {(docListResult.IsSuccess ? "OK" : $"FAIL ({docListResult.ErrorMessage})")}");
        if (docListResult.IsSuccess)
        {
            _logger.LogInformation("[AUTO-DOC] get_group_documents: OK data={Summary}", docListResult.GetDataSummary());
        }
        else
        {
            _logger.LogWarning("[AUTO-DOC] get_group_documents: FAIL error={Error}", docListResult.ErrorMessage ?? "unknown");
        }

        // Auto-Step 2: search_documents — intelligent query + document filtering
        // Chỉ search nếu có nội dung (tránh empty query error)
        if (!string.IsNullOrWhiteSpace(userQuestion))
        {
            // Extract document names from user question
            var mentionedDocNames = ExtractDocumentNamesFromQuestion(userQuestion);
            var matchedDocIds = new List<string>();

            if (mentionedDocNames.Count > 0)
            {
                // Match document names against the list
                matchedDocIds = MatchDocumentNamesAndExtractIds(mentionedDocNames, docListResult);
                reasoningSteps.Add($"[METHOD-A] Extracted {mentionedDocNames.Count} document name(s): {string.Join(", ", mentionedDocNames)}");
                
                if (matchedDocIds.Count > 0)
                {
                    reasoningSteps.Add($"[METHOD-A] Matched {matchedDocIds.Count} document ID(s). Filtering Qdrant search...");
                    _logger.LogInformation("[AUTO-DOC] Matched documents: {Ids}", string.Join(",", matchedDocIds));
                }
            }

            // Build search parameters
            var searchParams = new JsonObject
            {
                ["query"] = JsonValue.Create(userQuestion),
                ["top_k"] = JsonValue.Create(5)  // Get top 5 chunks
            };

            // Add document_id filter if we matched specific documents
            if (matchedDocIds.Count > 0)
            {
                // If multiple matches, use the first one (can be extended to support comma-separated)
                searchParams["document_id"] = JsonValue.Create(matchedDocIds[0]);
                reasoningSteps.Add($"[METHOD-A] search_documents with document_id={matchedDocIds[0]}, top_k=5");
            }

            var searchResult = await ExecuteToolAsync(
                "search_documents",
                searchParams,
                context,
                cancellationToken);
            history.AddCall("search_documents", searchParams, searchResult);
            reasoningSteps.Add($"[METHOD-A] search_documents: {(searchResult.IsSuccess ? "OK" : $"FAIL ({searchResult.ErrorMessage})")}");
            if (searchResult.IsSuccess)
            {
                _logger.LogInformation("[AUTO-DOC] search_documents: OK data={Summary}", searchResult.GetDataSummary());
            }
            else
            {
                _logger.LogWarning("[AUTO-DOC] search_documents: FAIL error={Error}", searchResult.ErrorMessage ?? "unknown");
            }
        }
        else
        {
            reasoningSteps.Add("[METHOD-A] search_documents: Skipped (empty question)");
        }
    }

    /// <summary>
    /// LLM quyết định action tiếp theo
    /// </summary>
    private async Task<AgentDecision> DecideActionAsync(
        string userQuestion,
        string systemPrompt,
        JsonObject toolsManifest,
        ToolExecutionHistory history,
        AIQueryContext context,
        CancellationToken cancellationToken,
        bool isContinuation = false,
        int consecutiveDecideWithoutExecution = 0)
    {
        // Build prompt với context
        var promptBuilder = new System.Text.StringBuilder();

        // LUON include system prompt de LLM thay cac QUY TAC CHON TOOL (critical!)
        // Va LUON include tools manifest de LLM biet cac tool nao ton tai
        promptBuilder.AppendLine(systemPrompt);
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("=== AVAILABLE TOOLS ===");
        promptBuilder.AppendLine(toolsManifest["tools"]?.ToString() ?? "[]");
        promptBuilder.AppendLine();

        promptBuilder.AppendLine("=== USER QUESTION ===");
        promptBuilder.AppendLine(userQuestion);
        promptBuilder.AppendLine();

        if (history.Calls.Count > 0)
        {
            promptBuilder.AppendLine("=== TOOL RESULTS (Recent Calls) ===");
            
            // Chỉ lấy 3 lần gọi cuối cùng để tránh context overflow
            var recentCalls = history.Calls.TakeLast(3).ToList();
            if (recentCalls.Count < history.Calls.Count)
            {
                promptBuilder.AppendLine($"[Note: Showing last {recentCalls.Count} of {history.Calls.Count} tool calls]");
            }
            
            // Context tracking per tool
            var toolContextLog = new System.Text.StringBuilder();
            toolContextLog.AppendLine("[CONTEXT-PER-TOOL]");
            int cumulativeChars = 0;
            
            foreach (var call in recentCalls)
            {
                promptBuilder.AppendLine($"Tool: {call.ToolName}");
                promptBuilder.AppendLine($"Parameters: {call.Parameters}");
                
                var fullResultJson = call.Result.ToJson();
                var resultJson = BuildCompactToolResultForPrompt(call.Result);
                var resultLength = resultJson.Length;
                cumulativeChars += resultLength;
                
                // Log context breakdown per tool
                toolContextLog.AppendLine(
                    $"Tool={call.ToolName} CharsIncluded={resultLength} CharsOriginal={fullResultJson.Length} CumulativeChars={cumulativeChars}");
                    
                promptBuilder.AppendLine($"Result: {resultJson}");
                promptBuilder.AppendLine();
            }
            
            _logger.LogInformation(toolContextLog.ToString());
        }

        promptBuilder.AppendLine("=== INSTRUCTIONS ===");
        if (history.Calls.Count == 0)
        {
            promptBuilder.AppendLine("- Analyze the question carefully");
            promptBuilder.AppendLine("- If you need data, call the appropriate tool(s)");
            promptBuilder.AppendLine("- If you have enough information, provide the answer directly");
            promptBuilder.AppendLine("- If the user asks for a generic task list ('task list', 'all tasks', 'group tasks', 'show tasks in this group'), call get_tasks with query/search empty and no filter fields unless a filter is explicitly requested.");
            promptBuilder.AppendLine("- Use query/search only for explicit keyword search across task title or description. Never use query/search to represent a filter intent.");
            promptBuilder.AppendLine("- If the user asks for any filter, you MUST use structured filter fields. Do not answer a filter question with an unfiltered task list.");
            promptBuilder.AppendLine("- Filter mapping rules:");
            promptBuilder.AppendLine("  * completed tasks / tasks done / tasks finished => status_category = Completed");
            promptBuilder.AppendLine("  * not started tasks / todo tasks => status_category = NotStarted");
            promptBuilder.AppendLine("  * in progress tasks / doing tasks => status_category = InProgress");
            promptBuilder.AppendLine("  * exact priority high / exact priority medium / exact priority low => priority = High/Medium/Low");
            promptBuilder.AppendLine("  * priority cao / priority high => priority = High");
            promptBuilder.AppendLine("  * priority trung binh / medium or higher / tu muc trung binh tro len => min_priority = Medium");
            promptBuilder.AppendLine("  * priority cao hon / high and above / high or higher / tu muc cao tro len => min_priority = High");
            promptBuilder.AppendLine("  * priority medium and above / medium or higher => min_priority = Medium");
            promptBuilder.AppendLine("  * priority high and above / high or higher => min_priority = High");
            promptBuilder.AppendLine("  * exact severity critical / exact severity major / exact severity moderate / exact severity minor => severity = Critical/Major/Moderate/Minor");
            promptBuilder.AppendLine("  * severity moderate and above / medium or higher severity => min_severity = Moderate");
            promptBuilder.AppendLine("  * severity high and above / high or higher severity / muc do cao tro len / muc cao tro len / tu muc cao tro len => min_severity = Major");
            promptBuilder.AppendLine("- Example tool calls:");
            promptBuilder.AppendLine("  * {\"tool_name\":\"get_tasks\",\"parameters\":{}} for a generic list request.");
            promptBuilder.AppendLine("  * {\"tool_name\":\"get_tasks\",\"parameters\":{\"status_category\":\"Completed\"}} for completed tasks.");
            promptBuilder.AppendLine("  * {\"tool_name\":\"get_tasks\",\"parameters\":{\"min_priority\":\"Medium\"}} for medium and above priority.");
            promptBuilder.AppendLine("  * {\"tool_name\":\"get_tasks\",\"parameters\":{\"min_priority\":\"High\"}} for high and above priority.");
            promptBuilder.AppendLine("  * {\"tool_name\":\"get_tasks\",\"parameters\":{\"min_severity\":\"Major\"}} for high severity and above.");
            promptBuilder.AppendLine("- Neu get_tasks tra ve phan trang (current_page, total_pages, page_size), hay hien thi ro cac thong tin nay cho user.");
            promptBuilder.AppendLine("- IMPORTANT - JSON FORMAT: Your response MUST be a valid single-line JSON object.");
            promptBuilder.AppendLine("- Tool execution policy: a tool may be retried only if its previous call failed. Never call the same tool again after it has succeeded in the current turn.");
            promptBuilder.AppendLine("  - Tool call: {\"action\": \"tool_call\", \"tool_name\": \"tool_name_here\", \"parameters\": {\"key\": \"value\"}}");
            promptBuilder.AppendLine("  - Final answer: {\"action\": \"answer\", \"final_answer\": \"your answer text here\"}");
            promptBuilder.AppendLine("  - NEVER omit the parameters field. If tool needs no params, use {\"parameters\": {}}");
            promptBuilder.AppendLine("- final_answer: chi la van ban thuan tuy. Khong dat trong ```, khong dat trong JSON object. Neu can xuong dong, dung \\n. Khong dung danh sach bullet dac biet.");
            promptBuilder.AppendLine("- DOCUMENT SEARCH STRATEGY:");
            promptBuilder.AppendLine("  * get_group_documents: ONLY to LIST file names/metadata. Does NOT search content.");
            promptBuilder.AppendLine("  * search_documents: For finding CONTENT within documents. Use with: query (required), document_id (optional), top_k (optional).");
            promptBuilder.AppendLine("  * If user asks about document CONTENT (\"what is...\", \"find...\", \"search in...\") -> use search_documents with semantic query.");
            promptBuilder.AppendLine("  * If user mentions specific file like \"2003.txt\" -> extract filename and use search_documents with document_id filter.");
            promptBuilder.AppendLine("- search_documents EXAMPLES:");
            promptBuilder.AppendLine("  * Generic content search: {\"query\": \"cac neu can tim kiem\"}");
            promptBuilder.AppendLine("  * Search in specific file: {\"query\": \"cac yeu cau\", \"document_id\": \"2003.txt\"}");
            promptBuilder.AppendLine("  * With result limit: {\"query\": \"...\", \"top_k\": 5}");

        }
        else
        {
            promptBuilder.AppendLine("- Based on the tool results, decide:");
            promptBuilder.AppendLine("  1. Call another tool if you need more data");
            promptBuilder.AppendLine("- Do not call get_tasks again after it has succeeded once in this turn. Use the returned data to answer immediately.");
            promptBuilder.AppendLine("- If the tool result is an unfiltered task list and the user actually asked for a filter, you should refine only once by calling get_tasks with the correct structured filters.");
            promptBuilder.AppendLine("- After any successful get_tasks call, do not call get_tasks again in the same turn. Use the returned data to answer immediately.");
            promptBuilder.AppendLine("- Neu get_tasks tra ve phan trang (current_page, total_pages, page_size), final_answer PHAI hien thi ro cac thong tin nay.");
            promptBuilder.AppendLine("- IMPORTANT - JSON FORMAT: Your response MUST be a valid single-line JSON object.");
            promptBuilder.AppendLine("- Tool execution policy: a tool may be retried only if its previous call failed. Never call the same tool again after it has succeeded in the current turn.");
            promptBuilder.AppendLine("  - Tool call: {\"action\": \"tool_call\", \"tool_name\": \"tool_name_here\", \"parameters\": {\"key\": \"value\"}}");
            promptBuilder.AppendLine("  - Final answer: {\"action\": \"answer\", \"final_answer\": \"your answer text here\"}");
            promptBuilder.AppendLine("  - NEVER omit the parameters field. If tool needs no params, use {\"parameters\": {}}");
            promptBuilder.AppendLine("- final_answer: chi la van ban thuan tuy. Khong dat trong ```, khong dat trong JSON object. Neu can xuong dong, dung \\n. Khong dung danh sach bullet dac biet.");

            // Hint: neu search_documents that bai vi query rong -> goi get_group_documents truoc
            if (history.Calls.Any(c => c.ToolName == "search_documents" && !c.Result.IsSuccess))
            {
                promptBuilder.AppendLine("- PREVIOUS FAILURE: search_documents that bai vi thieu query. "
                    + "Dieu nay xay ra vi ban khong biet trong nhom co nhung tai lieu gi. "
                    + "Goi get_group_documents (khong can tham so) de lay danh sach tai lieu co san. "
                    + "Sau do dua tren danh sach do, ban se biet phai tim kiem noi dung gi.");
            }

            // Hint: neu chua goi get_tasks thanh cong ma cau hoi lien quan den task
            bool hasCalledGetTasks = history.Calls.Any(c => c.ToolName == "get_tasks" && c.Result.IsSuccess);
            if (!hasCalledGetTasks)
            {
                promptBuilder.AppendLine("- IMPORTANT: Neu cau hoi lien quan den CONG VIEC (task, deadline, tien do, "
                    + "thanh vien, diem score) ma CHUA goi get_tasks -> GOI GET_TASKS NGAY. "
                    + "Ket qua tu documents (get_group_documents/search_documents) KHONG phai la task data. "
                    + "Phai goi get_tasks de lay danh sach cong viec.");
            }

            // Neu da co du lieu, chan LLM goi tiep cac tool tuy y
            if (history.Calls.Any(c => c.Result.IsSuccess))
            {
                promptBuilder.AppendLine("- CRITICAL: Ban DA CO du lieu tu tool calls truoc do. "
                    + "Neu cau hoi la danh sach task hoac list tasks va get_tasks da tra ket qua -> "
                    + "TRA LOI NGAY bang JSON. KHONG goi them tool nao nua.");
            }
        }

        // Gọi LLM
        var prompt = promptBuilder.ToString();
        
        var estimatedTokens = (int)(prompt.Length * _config.TokensPerCharacter);
        var wasContextTrimmed = false;
        var softMaxContextTokens = (int)Math.Floor(_config.MaxContextTokens * _config.SoftLimitRatio);

        if (consecutiveDecideWithoutExecution >= MaxConsecutiveDecideWithoutExecution)
        {
            _logger.LogWarning(
                "[DECIDE-LIMIT] Consecutive DecideAction calls without execution reached {Count}. Forcing finalize.",
                consecutiveDecideWithoutExecution);
            return new AgentDecision
            {
                ShouldCallTool = false,
                FinalAnswer = "Da goi nhieu lan nhung khong co du lieu moi. Vui long dua ra cau tra loi dua tren thong tin hien co."
            };
        }

        // Analyze prompt breakdown to see which sections consume most context
        var promptBreakdown = new System.Text.StringBuilder("[CONTEXT-BREAKDOWN] Prompt sections:\n");
        var systemPromptSection = systemPrompt;
        var toolsSection = $"=== AVAILABLE TOOLS ===\n{toolsManifest["tools"]?.ToString() ?? "[]"}";
        var questionSection = $"=== USER QUESTION ===\n{userQuestion}";
        var resultsSection = "";
        var instructionsSection = "";
        
        // Try to measure each section
        if (prompt.Contains("=== TOOL RESULTS"))
        {
            var resultsIdx = prompt.IndexOf("=== TOOL RESULTS");
            var instructionsIdx = prompt.IndexOf("=== INSTRUCTIONS ===");
            if (instructionsIdx > resultsIdx)
            {
                resultsSection = prompt[resultsIdx..instructionsIdx];
            }
        }
        
        if (prompt.Contains("=== INSTRUCTIONS ==="))
        {
            var instructionsIdx = prompt.IndexOf("=== INSTRUCTIONS ===");
            instructionsSection = prompt[instructionsIdx..];
        }
        
        promptBreakdown.AppendLine($"SystemPrompt: {systemPromptSection.Length} chars");
        promptBreakdown.AppendLine($"ToolsManifest: {toolsSection.Length} chars");
        promptBreakdown.AppendLine($"UserQuestion: {questionSection.Length} chars");
        if (resultsSection.Length > 0)
            promptBreakdown.AppendLine($"ToolResults: {resultsSection.Length} chars");
        if (instructionsSection.Length > 0)
            promptBreakdown.AppendLine($"Instructions: {instructionsSection.Length} chars");
        promptBreakdown.AppendLine($"TOTAL: {prompt.Length} chars → {estimatedTokens} tokens");
        
        _logger.LogInformation(promptBreakdown.ToString());
        
        if (estimatedTokens > softMaxContextTokens)
        {
            wasContextTrimmed = true;
            _logger.LogWarning(
                "[CONTEXT-OVERFLOW] PromptLen={CharCount} EstimatedTokens={Tokens} SoftMaxTokens={SoftMax} HardMaxTokens={HardMax} Trimmed=true",
                prompt.Length, estimatedTokens, softMaxContextTokens, _config.MaxContextTokens);
            
            // Smart trim: cắt từ cuối INSTRUCTIONS section để giữ system prompt + tools + question
            // Điều này đảm bảo LLM vẫn thấy rules quan trọng
            var instructionsIdx = prompt.LastIndexOf("=== INSTRUCTIONS ===");
            var trimNote = $"[... TRUNCATED TO STAY WITHIN SOFT TOKEN LIMIT ({_config.SoftLimitRatio:P0} BUFFER) ...]";
            if (instructionsIdx > 0)
            {
                var maxCharLimit = (int)(softMaxContextTokens / _config.TokensPerCharacter);
                var keepPrefix = prompt[..instructionsIdx];
                if (keepPrefix.Length < maxCharLimit)
                {
                    // Giữ prefix + phần INSTRUCTIONS
                    prompt = keepPrefix + "\n[... INSTRUCTIONS TRUNCATED DUE TO SOFT TOKEN LIMIT ...]";
                }
                else
                {
                    // Cắt cả prefix
                    prompt = prompt[..(int)(softMaxContextTokens / _config.TokensPerCharacter)] + $"\n{trimNote}";
                }
            }
            else
            {
                // Fallback: cắt từ cuối
                prompt = prompt[..(int)(softMaxContextTokens / _config.TokensPerCharacter)] + $"\n{trimNote}";
            }
        }
        else
        {
            _logger.LogInformation(
                "[CONTEXT-OK] PromptLen={CharCount} EstimatedTokens={Tokens} SoftMaxTokens={SoftMax} HardMaxTokens={HardMax}",
                prompt.Length, estimatedTokens, softMaxContextTokens, _config.MaxContextTokens);
        }

        _logger.LogInformation(
            "[LLM-PROMPT] Step={Step} PromptTokens={Tokens} ToolCalls={Count} WasTrimmed={Trimmed}",
            history.Calls.Count, estimatedTokens, history.Calls.Count, wasContextTrimmed);

        var (response, usage) = await _llmService.GenerateAnswerWithUsageAsync(
            prompt,
            userQuestion,
            "", // No extra context needed
            cancellationToken);

        // Accumulate token usage across all LLM calls
        _currentTokenUsage = new TokenUsage(
            (_currentTokenUsage?.InputTokens ?? 0) + usage.InputTokens,
            (_currentTokenUsage?.OutputTokens ?? 0) + usage.OutputTokens,
            (_currentTokenUsage?.CachedTokens ?? 0) + usage.CachedTokens,
            (_currentTokenUsage?.ThinkingTokens ?? 0) + usage.ThinkingTokens);

        _logger.LogInformation(
            "[LLM-RESPONSE] Step={Step} IsContinuation={IsCont} ResponseLength={Len} TokenUsage=In:{In} Out:{Out} Cached:{Cached} Thoughts:{Thoughts}",
            history.Calls.Count,
            isContinuation,
            response.Length,
            usage.InputTokens, usage.OutputTokens, usage.CachedTokens, usage.ThinkingTokens);

        // Parse response để quyết định action
        return ParseDecision(response, toolsManifest);
    }

    /// <summary>
    /// Thực thi tool - resolve fresh instance từ request scope để tránh DbContext disposed
    /// </summary>
    private async Task<AIQueryResult> ExecuteToolAsync(
        string toolName,
        JsonObject parameters,
        AIQueryContext context,
        CancellationToken cancellationToken)
    {
        // Lấy TYPE từ registry (không dùng instance cũ)
        _logger.LogInformation("[TOOL-EXEC-START] Tool={Tool} Params={Params}", toolName, parameters.ToString());
        var toolType = _toolRegistry.GetToolType(toolName);
        if (toolType == null)
        {
            return AIQueryResult.Error($"Tool '{toolName}' không tồn tại");
        }

        // Resolve fresh instance từ request scope - tránh disposed DbContext
        using var scope = _serviceProvider.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService(toolType) as IAITool;
        if (tool == null)
        {
            return AIQueryResult.Error($"Tool '{toolName}' không resolve được");
        }

        // Validate parameters before execution
        if (!tool.ValidateParameters(parameters))
        {
            // Chỉ log các params mà tool thực sự có trong schema
            var schema = tool.ParametersSchema;
            var neededParams = new List<string>();
            if (schema.TryGetPropertyValue("properties", out var props) && props is JsonObject propsObj)
            {
                foreach (var prop in propsObj)
                {
                    var key = prop.Key;
                    var val = parameters.TryGetPropertyValue(key, out var v) ? v?.ToString() ?? "null" : "(missing)";
                    neededParams.Add($"{key}='{val}'");
                }
            }
            var paramsStr = string.Join(" ", neededParams);
            _logger.LogWarning(
                "[TOOL-INVALID-PARAMS] Tool={ToolName} params=[{Params}]",
                toolName, paramsStr);
            return AIQueryResult.Error($"Tham so khong hop le: {paramsStr}");
        }

        try
        {
            // Use tool cache for better performance - cache hit returns immediately
            var result = await _toolCacheService.ExecuteWithCacheAsync(
                tool, context.UserId, context.GroupId, parameters, context, cancellationToken);
            
            // Log tool result - truncate to first 100 characters
            var resultData = result.Data?.ToString() ?? "";
            var truncatedResult = resultData.Length > 100 
                ? resultData[..100] + "..." 
                : resultData;
            
            _logger.LogWarning(
                "[TOOL-RESULT] Tool={ToolName} Success={Success} TimeMs={TimeMs} Result={Result}",
                toolName, result.IsSuccess, result.ExecutionTimeMs, truncatedResult);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool execution error: {ToolName}", toolName);
            return AIQueryResult.Error("Đã xảy ra lỗi khi thực hiện thao tác.");
        }
    }

    /// <summary>
    /// Parse LLM response để quyết định action
    /// </summary>
    private AgentDecision ParseDecision(string response, JsonObject toolsManifest)
    {
        // Guard: JSON bị cắt (streaming bị interrupt) — phát hiện bằng dấu hiệu:
        // - Chứa dấu `{` mở nhưng KHÔNG có `}` đóng hợp lệ
        // - Thường xảy ra ở cuối stream — response bị cắt giữa chừng
        if (response.Contains('{') && !response.TrimEnd().EndsWith('}'))
        {
            _logger.LogWarning(
                "[PARSE-TRUNCATED] LLM response truncated (JSON incomplete). Length={Len} Preview={Preview}",
                response.Length, response.Length > 200 ? response[..200] + "..." : response);

            // Thử extract action từ phần đã nhận được
            string? partialAction = null;
            foreach (var line in response.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Contains("\"action\""))
                {
                    var colonIdx = trimmed.IndexOf(':');
                    if (colonIdx >= 0)
                    {
                        var val = trimmed[(colonIdx + 1)..].Trim().Trim(',', '"', ' ', '{', '}');
                        if (val.StartsWith("tool_call")) partialAction = "tool_call";
                        else if (val.StartsWith("answer")) partialAction = "answer";
                    }
                }
            }

            if (partialAction == "tool_call")
            {
                // Truncated tool_call → ask LLM to retry with complete JSON
                return new AgentDecision
                {
                    ShouldCallTool = false,
                    FinalAnswer = "[INTERNAL_RETRY: JSON bi cat. Vui long goi lai tool voi JSON day du, dung format: {\"action\": \"tool_call\", \"tool_name\": \"...\", \"parameters\": {\"query\": \"...\"}}]"
                };
            }

            // Không extract được gì → fallback
            return new AgentDecision
            {
                ShouldCallTool = false,
                FinalAnswer = "Xin loi, phan hoi bi cat giua chung. Vui long thu lai."
            };
        }

        try
        {
            // Thử parse JSON
            var json = JsonSerializer.Deserialize<JsonElement>(response);

            if (json.TryGetProperty("action", out var actionElement))
            {
                var action = actionElement.GetString();

                if (action == "tool_call")
                {
                    var toolName = json.TryGetProperty("tool_name", out var tn) ? tn.GetString() : null;
                    var parameters = json.TryGetProperty("parameters", out var p) ?
                        JsonSerializer.Deserialize<JsonObject>(p.GetRawText()) ?? new JsonObject() :
                        new JsonObject();

                    return new AgentDecision
                    {
                        ShouldCallTool = !string.IsNullOrEmpty(toolName),
                        ToolName = toolName,
                        ToolParameters = parameters
                    };
                }
                else if (action == "answer")
                {
                    var fa = json.TryGetProperty("final_answer", out var faVal) ? faVal.GetString() : null;
                    return new AgentDecision
                    {
                        ShouldCallTool = false,
                        FinalAnswer = !string.IsNullOrWhiteSpace(fa) ? fa : response
                    };
                }
            }

            // Fallback: nếu không parse được JSON, coi như final answer
            return new AgentDecision
            {
                ShouldCallTool = false,
                FinalAnswer = response
            };
        }
        catch (Exception ex)
        {
            // Nếu parse fail, coi response là final answer nhưng log để debug
            _logger.LogWarning(ex, "ParseDecision: LLM response is not valid JSON. Raw length: {Len}, Raw: {Raw}",
                response.Length, response.Length > 200 ? response[..200] + "..." : response);
            return new AgentDecision
            {
                ShouldCallTool = false,
                FinalAnswer = response
            };
        }
    }

    private string GetSystemPromptVi() => @"Bạn là trợ lý AI của Study Studio - nền tảng học tập nhóm.

## NGỮ CẢNH
- ĐÂY LÀ GROUP AI: user đang ở trong một nhóm cụ thể.
- group_id ĐÃ ĐƯỢC CUNG CẤP TỰ ĐỘNG bởi hệ thống. KHÔNG cần hỏi user về group_id.
- CÁC TOOL bên dưới sẽ tự động nhận group_id từ hệ thống. KHÔNG truyền group_id/studio_id trong parameters.

## CÁCH HOẠT ĐỘNG
1. Đọc câu hỏi → phân loại: câu hỏi về CÔNG VIỆC hay TÀI LIỆU?
2. Nếu CÔNG VIỆC → dùng get_tasks, get_group_stats, get_deadlines TRƯỚC (KHÔNG cần tài liệu)
3. Nếu TÀI LIỆU → dùng get_group_documents → search_documents
4. Nếu đủ thông tin → trả lời

## QUAN TRỌNG: CHỌN TOOL ĐÚNG
### Câu hỏi về CÔNG VIỆC (dùng TRƯỚC TIÊN, không cần tài liệu):
- ""công việc"", ""task"", ""việc cần làm"", ""deadline"", ""hoàn thành"", ""tiến độ"", ""ai làm gì"", ""phân công"", ""kết quả"", ""thống kê"", ""score"", ""điểm"", ""xếp hạng"", ""priority"", ""severity"", ""bài tập""
→ Gọi: get_tasks, get_group_stats, get_deadlines, get_members

### Câu hỏi về TÀI LIỆU (chỉ khi hỏi về file cụ thể):
- ""tài liệu"", ""file"", ""document"", ""nội dung"", ""viết về"", ""báo cáo"", ""slide"", ""PDF""
→ Gọi: get_group_documents → search_documents (với query cụ thể)

### Câu hỏi về THÀNH VIÊN:
- ""thành viên"", ""member"", ""ai tham gia"", ""danh sách""
→ Gọi: get_members

## BẢNG CHỌN TOOL (BẮT BUỘC TUÂN THEO):
| User hỏi về... | Gọi tool NÀY TRƯỚC |
|---|---|
| Danh sách task, tiến độ, hoàn thành | get_tasks |
| Thống kê nhóm, tổng quan | get_group_stats |
| Deadline, ngày đến hạn | get_deadlines |
| Thành viên nhóm, ai làm gì | get_members |
| Tài liệu, file, tìm kiếm nội dung | get_group_documents + search_documents |

## TRÍCH DẪN TÀI LIỆU (BẮT BUỘC):
Khi trả lời từ search_documents, BẮT BUỘC ghi rõ nguồn:
- Viết: ""Câu trả lời dựa trên [tên_file]"" hoặc ""Theo [tên_file]""
- KHÔNG BAO GIỜ trả lời từ tài liệu mà không ghi tên file
- Nếu nhiều file → trích dẫn từng file: ""Theo [file1] và [file2]...""

## LỖI THƯỜNG GẶP - TRÁNH XA:
- ""Tham so khong hop le"" = LLM gọi tool nhưng THIẾU hoặc SAI tham số bắt buộc (query)
- ""Khong co quyen"" = User không phải thành viên nhóm
- KHÔNG BAO GIỜ hỏi user về group_id, studio_id, hay yêu cầu cung cấp thông tin đã có sẵn
- KHÔNG dùng search_documents cho câu hỏi về công việc

## QUY TẮC
- Câu hỏi về CÔNG VIỆC → dùng task tools TRƯỚC, tài liệu KHÔNG cần thiết
- Câu hỏi về TÀI LIỆU → luôn trích dẫn tên file nguồn
- Chỉ gọi tool khi thực sự cần data
- Trả lời bằng tiếng Việt
- Trung thực, không bịa đặt thông tin
- Nếu data không đủ, nói rõ là không đủ thông tin

## SCORING KNOWLEDGE (Cơ chế tính điểm)

### Priority & Severity
- Priority (Ưu tiên): Low (x1.0), Medium (x1.5), High (x2.0)
- Severity (Mức độ): Minor (x1.0), Moderate (x1.2), Major (x1.5), Critical (x2.0)

### Công thức Task hoàn thành
  Điểm = 10 × PriorityWeight × SeverityWeight
  - High + Critical: 10 × 2.0 × 2.0 = 40 điểm
  - Medium + Major:  10 × 1.5 × 1.5 = 22.5 điểm
  - Low + Minor:     10 × 1.0 × 1.0 = 10 điểm

### Các action khác (flat - không nhân)
  - Tạo Task mới: +3 điểm
  - Cập nhật Task: +1 điểm

### Activity Level (ngưỡng tích lũy)
  | Level | Điểm số     | Nhãn      |
  |-------|-------------|-----------|
  | 1     | 0 < s ≤ 5   | Low       |
  | 2     | 5 < s ≤ 15  | Medium    |
  | 3     | 15 < s ≤ 30 | High      |
  | 4     | > 30        | Very High |

### Cách trả lời về điểm số
- Khi user hỏi ""điểm"", ""score"", ""xếp hạng"" → giải thích công thức + áp dụng vào task data từ tools
- ""Task này bao nhiêu điểm?"" → lấy Priority/Severity từ task data + công thức trên
- Dùng priority_breakdown + severity_breakdown từ get_group_stats để phân tích phân bố độ khó công việc

## FORMAT TRẢ LỜI
Luôn trả lời dưới dạng JSON:
- Tool call: {""action"": ""tool_call"", ""tool_name"": ""tên_tool"", ""parameters"": {""key"": ""value""}}
- Final answer: {""action"": ""answer"", ""final_answer"": ""nội dung câu trả lời""}";

    private string GetSystemPromptEn() => @"You are an AI assistant for Study Studio - a group learning platform.

## CONTEXT
- THIS IS GROUP AI: user is inside a specific group.
- group_id IS AUTOMATICALLY PROVIDED by the system. DO NOT ask user for group_id.
- Tools below will automatically receive group_id from the system. DO NOT pass group_id/studio_id in parameters.

## HOW IT WORKS
1. Read user's question → classify: TASK question or DOCUMENT question?
2. If TASK question → use get_tasks, get_group_stats, get_deadlines FIRST (NOT documents)
3. If DOCUMENT question → use get_group_documents → search_documents
4. If you have enough info → provide answer

## CRITICAL: CHOOSE THE RIGHT TOOL
### TASK questions (use these FIRST, NOT documents):
- ""công việc"", ""task"", ""việc cần làm"", ""deadline"", ""hoàn thành"", ""tiến độ"", ""ai làm gì"", ""phân công"", ""kết quả"", ""thống kê"", ""score"", ""điểm"", ""xếp hạng"", ""priority"", ""severity""
→ Call: get_tasks, get_group_stats, get_deadlines, get_members

### DOCUMENT questions (only for specific file/content questions):
- ""tài liệu"", ""file"", ""document"", ""nội dung"", ""viết về"", ""báo cáo"", ""slide"", ""PDF""
→ Call: get_group_documents → search_documents (with specific query)

### MEMBERS questions:
- ""thành viên"", ""member"", ""ai tham gia"", ""danh sách""
→ Call: get_members

## WHEN TO USE WHICH TOOL (MUST FOLLOW):
| User asks about... | Call this tool FIRST |
|---|---|
| Task list, progress, completion | get_tasks |
| Task statistics, overview | get_group_stats |
| Deadlines, due dates | get_deadlines |
| Group members, who does what | get_members |
| Documents, files, content search | get_group_documents + search_documents |

## DOCUMENT CITATION (MANDATORY):
When answering from search_documents results, you MUST cite the source:
- Write: ""The answer is based on [document_name]"" or ""According to [document_name]""
- NEVER answer from documents without naming the source file
- If multiple documents contribute → cite each one: ""According to [doc1] and [doc2]...""

## COMMON ERRORS - AVOID:
- ""Tham so khong hop le"" = LLM called tool but MISSING or WRONG required parameter (e.g., query is null)
- ""Khong co quyen"" = User is not a member of the group
- NEVER ask user for group_id, studio_id, or information already available
- DO NOT use search_documents for task-related questions

## RULES
- TASK questions → use task tools FIRST, documents are NOT needed
- DOCUMENT questions → always cite the source file name
- Only call tools when you really need data
- Answer in English
- Be honest, don't fabricate information
- If data is insufficient, clearly state it

## SCORING KNOWLEDGE

### Priority & Severity
- Priority (Urgency): Low (x1.0), Medium (x1.5), High (x2.0)
- Severity (Impact): Minor (x1.0), Moderate (x1.2), Major (x1.5), Critical (x2.0)

### Task Completion Score
  Score = 10 × PriorityWeight × SeverityWeight
  - High + Critical: 10 × 2.0 × 2.0 = 40 points
  - Medium + Major:  10 × 1.5 × 1.5 = 22.5 points
  - Low + Minor:     10 × 1.0 × 1.0 = 10 points

### Other Actions (flat, no multiplier)
  - Create Task: +3 points
  - Update Task: +1 point

### Activity Level Thresholds (cumulative)
  | Level | Score Range | Label      |
  |-------|-------------|------------|
  | 1     | 0 < s ≤ 5   | Low        |
  | 2     | 5 < s ≤ 15  | Medium     |
  | 3     | 15 < s ≤ 30 | High       |
  | 4     | > 30        | Very High  |

### How to Answer Score Questions
- When user asks ""score"", ""points"", ""ranking"" → explain formula + apply to task data from tools
- ""How many points is this task worth?"" → use Priority + Severity from task data + formula above
- Use priority_breakdown + severity_breakdown from get_group_stats to analyze task difficulty distribution

## RESPONSE FORMAT
Always respond in JSON:
- Tool call: {""action"": ""tool_call"", ""tool_name"": ""tool_name"", ""parameters"": {""key"": ""value""}}
- Final answer: {""action"": ""answer"", ""final_answer"": ""your answer""}";

    /// <summary>
    /// Returns role-specific system prompt based on AIQueryContext.
    /// - StudioId set → Master AI (Studio Owner)
    /// - GroupId set → Group AI or Personal AI (member context)
    /// - Neither set → default prompt
    /// </summary>
    private string GetRoleSystemPrompt(AIQueryContext context)
    {
        bool isEn = context.Language.ToLower() == "en";
        if (context.StudioId.HasValue)
            return isEn ? _ownerSystemPromptEn : _ownerSystemPromptVi;
        // Personal AI: StudioId null + GroupId null → use Personal prompt
        if (!context.StudioId.HasValue && !context.GroupId.HasValue)
            return isEn ? _personalSystemPromptEn : _personalSystemPromptVi;
        // Group AI: GroupId set → use Group prompt (get_tasks, get_deadlines, etc.)
        return isEn ? _systemPromptEn : _systemPromptVi;
    }

    private string GetPersonalSystemPromptVi() => @"Bạn là trợ lý AI cá nhân của Study Studio, giúp bạn quản lý công việc và tiến độ học tập.

## VAI TRÒ
Bạn là trợ lý cá nhân tập trung vào:
- Giúp bạn xem và quản lý công việc cá nhân
- Theo dõi deadline và nhắc nhở
- Tổng hợp thống kê hiệu suất cá nhân
- Gợi ý cách cải thiện năng suất

## CÁC TOOLS CÓ SẴN (KHÔNG CẦN group_id)
- get_personal_tasks: Lấy danh sach tat ca cong viec (ca nhan va duoc assign)
- get_personal_deadlines: Lấy deadline cong viec ca nhan
- get_personal_stats: Lấy thong ke nang suất ca nhan

## QUY TẮC
- LUÔN gọi tool để lấy dữ liệu thực trước khi trả lời
- Trả lời bằng tiếng Việt
- Trung thực, không bịa đặt
- Nếu không có dữ liệu, nói rõ và gợi ý cách cải thiện

## SCORING KNOWLEDGE (Cơ chế tính điểm)

### Priority & Severity
- Priority (Ưu tiên): Low (x1.0), Medium (x1.5), High (x2.0)
- Severity (Mức độ): Minor (x1.0), Moderate (x1.2), Major (x1.5), Critical (x2.0)

### Công thức Task hoàn thành
  Điểm = 10 × PriorityWeight × SeverityWeight
  - High + Critical: 10 × 2.0 × 2.0 = 40 điểm
  - Medium + Major:  10 × 1.5 × 1.5 = 22.5 điểm
  - Low + Minor:     10 × 1.0 × 1.0 = 10 điểm

### Các action khác (flat - không nhân)
  - Tạo Task mới: +3 điểm
  - Cập nhật Task: +1 điểm

### Activity Level (ngưỡng tích lũy)
  | Level | Điểm số     | Nhãn      |
  |-------|-------------|-----------|
  | 1     | 0 < s ≤ 5   | Low       |
  | 2     | 5 < s ≤ 15  | Medium    |
  | 3     | 15 < s ≤ 30 | High      |
  | 4     | > 30        | Very High |

### Cách trả lời về điểm số
- Khi user hỏi ""điểm"", ""score"" → dùng Priority/Severity từ get_personal_tasks + công thức trên
- ""Task này bao nhiêu điểm?"" → tính theo công thức
- Dùng priority_breakdown + severity_breakdown từ get_personal_stats để giải thích phân bố công việc

## FORMAT TRẢ LỜI
Luôn trả lời dưới dạng JSON:
- Tool call: {""action"": ""tool_call"", ""tool_name"": ""tên_tool"", ""parameters"": {""key"": ""value""}}
- Final answer: {""action"": ""answer"", ""final_answer"": ""nội dung câu trả lời""}";

    private string GetPersonalSystemPromptEn() => @"You are a personal AI assistant for Study Studio, helping you manage your tasks and learning progress.

## ROLE
You are a personal assistant focused on:
- Helping you view and manage personal tasks
- Tracking deadlines and reminders
- Summarizing your personal performance statistics
- Suggesting ways to improve productivity

## AVAILABLE TOOLS (NO group_id REQUIRED)
- get_personal_tasks: Get all tasks (personal and assigned)
- get_personal_deadlines: Get personal task deadlines
- get_personal_stats: Get personal productivity stats

## RULES
- ALWAYS call a tool to get real data before answering
- Answer in English
- Be honest, don't fabricate
- If no data available, say so clearly

## SCORING KNOWLEDGE

### Priority & Severity
- Priority (Urgency): Low (x1.0), Medium (x1.5), High (x2.0)
- Severity (Impact): Minor (x1.0), Moderate (x1.2), Major (x1.5), Critical (x2.0)

### Task Completion Score
  Score = 10 × PriorityWeight × SeverityWeight
  - High + Critical: 10 × 2.0 × 2.0 = 40 points
  - Medium + Major:  10 × 1.5 × 1.5 = 22.5 points
  - Low + Minor:     10 × 1.0 × 1.0 = 10 points

### Other Actions (flat, no multiplier)
  - Create Task: +3 points
  - Update Task: +1 point

### Activity Level Thresholds
  | Level | Score Range | Label      |
  |-------|-------------|------------|
  | 1     | 0 < s ≤ 5   | Low        |
  | 2     | 5 < s ≤ 15  | Medium     |
  | 3     | 15 < s ≤ 30 | High       |
  | 4     | > 30        | Very High  |

### How to Answer Score Questions
- When user asks ""score"", ""points"" → use Priority + Severity from get_personal_tasks data + formula
- ""What is this task worth?"" → calculate using the formula above
- Use priority_breakdown + severity_breakdown from get_personal_stats to explain task distribution

## RESPONSE FORMAT
Always respond in JSON:
- Tool call: {""action"": ""tool_call"", ""tool_name"": ""tool_name"", ""parameters"": {""key"": ""value""}}
- Final answer: {""action"": ""answer"", ""final_answer"": ""your answer""}";

    private string GetOwnerSystemPromptVi() => @"Bạn là AI Quản lý Studio (Master AI) của Study Studio - dành cho chủ sở hữu Studio.

## VAI TRÒ
Bạn có quyền truy cập toàn bộ dữ liệu của Studio. studio_id ĐÃ ĐƯỢC CUNG CẤP TỰ ĐỘNG trong request context.
Bạn tập trung vào:
- Tổng hợp tình hình tất cả các nhóm trong Studio
- So sánh hiệu suất giữa các nhóm
- Phân tích rủi ro và cảnh báo sớm
- Đề xuất cải thiện cho toàn Studio

## QUAN TRỌNG: studio_id
studio_id đã được tự động cung cấp bởi hệ thống. KHI GỌI TOOL, KHÔNG CẦN truyền studio_id:
- Tool sẽ tự động nhận studio_id từ request context

## CÁC TOOLS CÓ SẴN

### Studio-level (không cần tham số - studio_id tự động từ context):
- get_studio_analytics: Thống kê tổng thể Studio (tổng nhóm, thành viên, task, hoàn thành, quá hạn)
- get_studio_groups: Danh sách tất cả nhóm kèm thống kê task
- get_studio_health: Điểm sức khoẻ tổng thể Studio (0-100)
- get_group_comparison: So sánh nhiều nhóm với nhau
- get_risk_groups: Xác định các nhóm có nguy cơ
- get_member_permissions: Kiểm tra quyền thành viên
- get_storage_usage: Kiểm tra dung lượng lưu trữ

### Group-level (dùng group_id để chỉ định nhóm cụ thể):
Bạn có quyền gọi các Group tools với parameter **group_id** để xem chi tiết từng nhóm.
- get_group_stats: Thống kê chi tiết một nhóm (tasks, completion, overdue) → parameter: group_id
- get_tasks: Danh sách task của một nhóm → parameter: group_id
- get_deadlines: Deadline của một nhóm → parameter: group_id
- get_members: Thành viên một nhóm → parameter: group_id
- get_group_performance: Hiệu suất một nhóm (priority/severity breakdown) → parameter: group_id
- get_group_documents: Tài liệu một nhóm → parameter: group_id
- get_group_risk: Đánh giá rủi ro một nhóm → parameter: group_id
- search_documents: Tìm kiếm tài liệu → parameter: query (bắt buộc), group_id (tùy chọn)

## KHI NÀO DÙNG TOOL NÀO:
- ""tóm tắt tiến độ"" / ""overview"" / ""tổng quan"" → get_studio_analytics
- ""nhóm nào"" / ""so sánh"" / ""performance"" → get_group_comparison
- ""cảnh báo"" / ""nguy cơ"" / ""rủi ro"" → get_risk_groups
- ""sức khoẻ"" / ""đánh giá studio"" → get_studio_health
- ""danh sách nhóm"" / ""xem nhóm"" → get_studio_groups
- ""thống kê nhóm X"" / ""task nhóm Y"" / ""thành viên nhóm Z"" → gọi Group tool + group_id

## QUY TẮC
- Trả lời bằng tiếng Việt
- studio_id: KHÔNG truyền (tự động từ context)
- group_id: TRUYỀN khi dùng Group tools (guid, ví dụ: ""d4735e2a-..."")
- Trung thực, không bịa đặt
- Dùng bảng markdown để so sánh nhóm
- Luôn đưa ra gợi ý cải thiện cụ thể

## FORMAT TRẢ LỜI
Luôn trả lời dưới dạng JSON:
- Tool call: {""action"": ""tool_call"", ""tool_name"": ""tên_tool"", ""parameters"": {}}
- Final answer: {""action"": ""answer"", ""final_answer"": ""nội dung câu trả lời""}

## SCORING KNOWLEDGE (Cơ chế tính điểm)

### Priority & Severity
- Priority (Ưu tiên): Low (x1.0), Medium (x1.5), High (x2.0)
- Severity (Mức độ): Minor (x1.0), Moderate (x1.2), Major (x1.5), Critical (x2.0)

### Công thức Task hoàn thành
  Điểm = 10 × PriorityWeight × SeverityWeight
  - High + Critical: 10 × 2.0 × 2.0 = 40 điểm
  - Medium + Major:  10 × 1.5 × 1.5 = 22.5 điểm
  - Low + Minor:     10 × 1.0 × 1.0 = 10 điểm

### Các action khác (flat - không nhân)
  - Tạo Task mới: +3 điểm
  - Cập nhật Task: +1 điểm

### Activity Level (ngưỡng tích lũy)
  | Level | Điểm số     | Nhãn      |
  |-------|-------------|-----------|
  | 1     | 0 < s ≤ 5   | Low       |
  | 2     | 5 < s ≤ 15  | Medium    |
  | 3     | 15 < s ≤ 30 | High      |
  | 4     | > 30        | Very High |

### Dùng scoring cho Studio
- Dùng Activity Level thresholds để đánh giá nhóm/thành viên
- Dùng priority_breakdown + severity_breakdown từ get_group_performance để phân tích nhóm nào có nhiều công việc khó
- Gợi ý cải thiện: nhóm có nhiều High+Critical tasks nhưng completion thấp → ưu tiên";

    private string GetOwnerSystemPromptEn() => @"You are a Studio Management AI (Master AI) for Study Studio - for Studio owners.

## ROLE
You have access to all Studio data. studio_id is AUTOMATICALLY PROVIDED in the request context.
You focus on:
- Overview of all groups in the Studio
- Comparing performance between groups
- Risk analysis and early warnings
- Improvement recommendations for the entire Studio

## CONTEXT
studio_id is AUTOMATICALLY PROVIDED in the request context. DO NOT pass studio_id in tool parameters.
As the Studio Owner, you CAN also call Group-level tools with **group_id** to inspect specific group details.

## AVAILABLE TOOLS

### Studio-level (no parameters - studio_id auto from context):
- get_studio_analytics: Overall Studio statistics (groups, members, tasks, completion, overdue)
- get_studio_groups: List all groups with task statistics
- get_studio_health: Overall Studio health score (0-100)
- get_group_comparison: Compare multiple groups
- get_risk_groups: Identify at-risk groups
- get_member_permissions: Check member permissions
- get_storage_usage: Check storage usage

### Group-level (pass group_id to inspect a specific group):
You have permission to call Group tools with parameter **group_id** for detailed group inspection.
- get_group_stats: Detailed group statistics (tasks, completion, overdue) → parameter: group_id
- get_tasks: Tasks in a group → parameter: group_id
- get_deadlines: Deadlines in a group → parameter: group_id
- get_members: Members of a group → parameter: group_id
- get_group_performance: Group performance (priority/severity breakdown) → parameter: group_id
- get_group_documents: Documents in a group → parameter: group_id
- get_group_risk: Risk assessment for a group → parameter: group_id
- search_documents: Search documents → parameter: query (required), group_id (optional)

## WHEN TO USE WHICH TOOL:
- ""summarize progress"" / ""overview"" → get_studio_analytics
- ""which group"" / ""compare"" / ""performance"" → get_group_comparison
- ""warning"" / ""risk"" / ""danger"" → get_risk_groups
- ""health"" / ""evaluate studio"" → get_studio_health
- ""group list"" / ""view groups"" → get_studio_groups
- ""stats for group X"" / ""tasks in group Y"" / ""members of group Z"" → Group tool + group_id

## RULES
- Answer in English
- studio_id: DO NOT pass (auto from context)
 - group_id: PASS when using Group tools (guid, e.g. ""d4735e2a-..."")
- Be honest, don't fabricate
- Use markdown tables for group comparisons
- Always provide specific improvement recommendations

## SCORING KNOWLEDGE

### Priority & Severity
- Priority (Urgency): Low (x1.0), Medium (x1.5), High (x2.0)
- Severity (Impact): Minor (x1.0), Moderate (x1.2), Major (x1.5), Critical (x2.0)

### Task Completion Score
  Score = 10 × PriorityWeight × SeverityWeight
  - High + Critical: 10 × 2.0 × 2.0 = 40 points
  - Medium + Major:  10 × 1.5 × 1.5 = 22.5 points
  - Low + Minor:     10 × 1.0 × 1.0 = 10 points

### Other Actions (flat, no multiplier)
  - Create Task: +3 points
  - Update Task: +1 point

### Activity Level Thresholds
  | Level | Score Range | Label      |
  |-------|-------------|------------|
  | 1     | 0 < s ≤ 5   | Low        |
  | 2     | 5 < s ≤ 15  | Medium     |
  | 3     | 15 < s ≤ 30 | High       |
  | 4     | > 30        | Very High  |

### How to Use Scoring for Studio Management
- Use Activity Level thresholds to evaluate groups and members
- Use priority_breakdown + severity_breakdown from get_group_performance to analyze which groups have difficult tasks
- Recommend improvement: groups with many High+Critical tasks but low completion rate should be prioritized

## RESPONSE FORMAT
Always respond in JSON:
- Tool call: {""action"": ""tool_call"", ""tool_name"": ""tool_name"", ""parameters"": {}}
- Final answer: {""action"": ""answer"", ""final_answer"": ""your answer""}";
}

/// <summary>
/// Kết quả trả về từ AIAgent
/// </summary>
public class AIAgentResult
{
    public string Answer { get; set; } = "";
    public List<string> ReasoningSteps { get; set; } = new();
    public List<ToolCallEntry> ToolCalls { get; set; } = new();
    public long ProcessingTimeMs { get; set; }
    public int ToolCallCount { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    /// <summary>
    /// Lý do khi fallback fire — null nếu LLM trả lời thật.
    /// </summary>
    public string? FallbackReason { get; set; }

    // Token usage from Gemini usage_metadata (for accurate analytics)
    public TokenUsage? TokenUsage { get; set; }
}
/// <summary>
/// Quyết định của Agent
/// </summary>
public class AgentDecision
{
    public bool ShouldCallTool { get; set; }
    public string? ToolName { get; set; }
    public JsonObject? ToolParameters { get; set; }
    public string? FinalAnswer { get; set; }
}

/// <summary>
/// SSE chunk for streaming AI responses
/// </summary>
public class AIStreamChunk
{
    public string Type { get; set; } = ""; // metadata, chunk, done, error
    public string? Content { get; set; }
    public int? RemainingRequests { get; set; }
    public int? DailyLimit { get; set; }
    public int? ToolCount { get; set; }
    public long? ProcessingTimeMs { get; set; }
    public string? ErrorMessage { get; set; }

    // Token usage from Gemini usage_metadata (for analytics)
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? CachedTokens { get; set; }
    public int? ThinkingTokens { get; set; }
}

/// <summary>
/// Internal result for streaming processing
/// </summary>
internal class AIStreamResult
{
    public int ToolCount { get; set; }
    public long ProcessingTimeMs { get; set; }
    public List<AIStreamChunk> Chunks { get; set; } = new();
    public TokenUsage? TokenUsage { get; set; }
}
