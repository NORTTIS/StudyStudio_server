using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudioStudio_Server.Services.AI.Interfaces;
using StudioStudio_Server.Services.AI.Models;

namespace StudioStudio_Server.Services.AI.Pipeline;

public partial class AIAgent
{
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

    private static AIReviewVerdict ReviewToolFit(AIIntentAnalysis intent, AgentDecision decision)
    {
        if (string.IsNullOrWhiteSpace(decision.ToolName))
        {
            return new AIReviewVerdict(false, "Missing tool name", "wrong_tool");
        }

        var toolName = decision.ToolName;

        if (intent.IsDocumentIntent && !intent.IsTaskIntent && IsTaskTool(toolName))
        {
            return new AIReviewVerdict(false, "User asked about documents but planned a task tool", "wrong_tool", "get_group_documents");
        }

        if (intent.IsTaskIntent && !intent.IsDocumentIntent && IsDocumentTool(toolName))
        {
            return new AIReviewVerdict(false, "User asked about tasks but planned a document tool", "wrong_tool", "get_tasks");
        }

        if (intent.Category == "personal" && !IsPersonalTool(toolName))
        {
            return new AIReviewVerdict(false, "Personal context should use personal tools", "wrong_tool", "get_personal_tasks");
        }

        if (intent.Category.StartsWith("group", StringComparison.OrdinalIgnoreCase) && IsPersonalTool(toolName))
        {
            return new AIReviewVerdict(false, "Group context should not use personal tools", "wrong_tool", "get_tasks");
        }

        return new AIReviewVerdict(true, "Tool fits user intent", "accepted", toolName);
    }

    private static bool ToolHasRequiredParameters(IAITool tool)
    {
        if (tool.ParametersSchema["required"] is not JsonArray requiredArray)
        {
            return false;
        }

        return requiredArray.Count > 0;
    }

    private static bool HasMeaningfulParameterValues(JsonObject parameters)
    {
        foreach (var prop in parameters)
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
                        return true;
                    }

                    continue;
                }

                return true;
            }

            if (node is JsonObject || node is JsonArray)
            {
                return true;
            }
        }

        return false;
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

        if (decision.ToolName.Equals("get_tasks", StringComparison.OrdinalIgnoreCase))
        {
            reviewedParameters = NormalizeGetTasksParameters(userQuestion, reviewedParameters);
        }

        if (reviewedParameters.Count == 0)
        {
            if (!ToolHasRequiredParameters(tool))
            {
                _logger.LogInformation("[PARAM-REVIEW] Tool {Tool} has no params and no required fields, auto-accepting", decision.ToolName);
                return new AIFlowDecision(
                    StepName: "parameter-review",
                    Decision: decision,
                    ToolParameters: reviewedParameters,
                    IsAccepted: true,
                    ReviewState: "accepted",
                    ReviewNote: "No parameters required");
            }

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

        var hasValidParams = HasMeaningfulParameterValues(reviewedParameters);

        if (!hasValidParams)
        {
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

        _logger.LogInformation("[PARAM-REVIEW] Tool {Tool} has valid params, skipping LLM review", decision.ToolName);
        return new AIFlowDecision(
            StepName: "parameter-review",
            Decision: decision,
            ToolParameters: reviewedParameters,
            IsAccepted: true,
            ReviewState: "accepted",
            ReviewNote: "Parameters validated (no LLM review needed)");
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

    private AgentDecision ParseDecision(string response, JsonObject toolsManifest)
    {
        response = ExtractJsonPayload(response);

        if (response.Contains('{') && !response.TrimEnd().EndsWith('}'))
        {
            _logger.LogWarning(
                "[PARSE-TRUNCATED] LLM response truncated (JSON incomplete). Length={Len} Preview={Preview}",
                response.Length, response.Length > 200 ? response[..200] + "..." : response);

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
                return new AgentDecision
                {
                    ShouldCallTool = false,
                    FinalAnswer = "[INTERNAL_RETRY: JSON bi cat. Vui long goi lai tool voi JSON day du, dung format: {\"action\": \"tool_call\", \"tool_name\": \"...\", \"parameters\": {\"query\": \"...\"}}]"
                };
            }

            return new AgentDecision
            {
                ShouldCallTool = false,
                FinalAnswer = "Xin loi, phan hoi bi cat giua chung. Vui long thu lai."
            };
        }

        try
        {
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

            return new AgentDecision
            {
                ShouldCallTool = false,
                FinalAnswer = response
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ParseDecision: LLM response is not valid JSON. Raw length: {Len}, Raw: {Raw}",
                response.Length, response.Length > 200 ? response[..200] + "..." : response);
            return new AgentDecision
            {
                ShouldCallTool = false,
                FinalAnswer = response
            };
        }
    }

    private static string ExtractJsonPayload(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return response;
        }

        var trimmed = response.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewLine = trimmed.IndexOf('\n');
            if (firstNewLine >= 0)
            {
                trimmed = trimmed[(firstNewLine + 1)..];
            }

            var closingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (closingFence >= 0)
            {
                trimmed = trimmed[..closingFence];
            }

            trimmed = trimmed.Trim();
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start >= 0 && end >= start)
        {
            return trimmed.Substring(start, end - start + 1);
        }

        return trimmed;
    }

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
        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine("=== AVAILABLE TOOLS ===");
        promptBuilder.AppendLine(toolsManifest["tools"]?.ToString() ?? "[]");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("=== USER QUESTION ===");
        promptBuilder.AppendLine(userQuestion);
        promptBuilder.AppendLine();

        if (history.Calls.Count > 0)
        {
            promptBuilder.AppendLine("=== TOOL RESULTS (Recent Calls) ===");
            var recentCalls = history.Calls.TakeLast(3).ToList();
            if (recentCalls.Count < history.Calls.Count)
            {
                promptBuilder.AppendLine($"[Note: Showing last {recentCalls.Count} of {history.Calls.Count} tool calls]");
            }

            var toolContextLog = new StringBuilder();
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

            if (history.Calls.Any(c => c.ToolName == "search_documents" && !c.Result.IsSuccess))
            {
                promptBuilder.AppendLine("- PREVIOUS FAILURE: search_documents that bai vi thieu query. "
                    + "Dieu nay xay ra vi ban khong biet trong nhom co nhung tai lieu gi. "
                    + "Goi get_group_documents (khong can tham so) de lay danh sach tai lieu co san. "
                    + "Sau do dua tren danh sach do, ban se biet phai tim kiem noi dung gi.");
            }

            bool hasCalledGetTasks = history.Calls.Any(c => c.ToolName == "get_tasks" && c.Result.IsSuccess);
            if (!hasCalledGetTasks)
            {
                promptBuilder.AppendLine("- IMPORTANT: Neu cau hoi lien quan den CONG VIEC (task, deadline, tien do, "
                    + "thanh vien, diem score) ma CHUA goi get_tasks -> GOI GET_TASKS NGAY. "
                    + "Ket qua tu documents (get_group_documents/search_documents) KHONG phai la task data. "
                    + "Phai goi get_tasks de lay danh sach cong viec.");
            }

            if (history.Calls.Any(c => c.Result.IsSuccess))
            {
                promptBuilder.AppendLine("- CRITICAL: Ban DA CO du lieu tu tool calls truoc do. "
                    + "Neu cau hoi la danh sach task hoac list tasks va get_tasks da tra ket qua -> "
                    + "TRA LOI NGAY bang JSON. KHONG goi them tool nao nua.");
            }
        }

        var prompt = promptBuilder.ToString();

        var estimatedTokens = (int)((prompt.Length + systemPrompt.Length) * _config.TokensPerCharacter);
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

        var promptBreakdown = new StringBuilder("[CONTEXT-BREAKDOWN] Prompt sections:\n");
        var systemPromptSection = systemPrompt;
        var toolsSection = $"=== AVAILABLE TOOLS ===\n{toolsManifest["tools"]?.ToString() ?? "[]"}";
        var questionSection = $"=== USER QUESTION ===\n{userQuestion}";
        var resultsSection = "";
        var instructionsSection = "";

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

            var instructionsIdx = prompt.LastIndexOf("=== INSTRUCTIONS ===");
            var trimNote = $"[... TRUNCATED TO STAY WITHIN SOFT TOKEN LIMIT ({_config.SoftLimitRatio:P0} BUFFER) ...]";
            if (instructionsIdx > 0)
            {
                var maxCharLimit = (int)(softMaxContextTokens / _config.TokensPerCharacter);
                var keepPrefix = prompt[..instructionsIdx];
                if (keepPrefix.Length < maxCharLimit)
                {
                    prompt = keepPrefix + "\n[... INSTRUCTIONS TRUNCATED DUE TO SOFT TOKEN LIMIT ...]";
                }
                else
                {
                    prompt = prompt[..(int)(softMaxContextTokens / _config.TokensPerCharacter)] + $"\n{trimNote}";
                }
            }
            else
            {
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
            systemPrompt,
            prompt,
            "",
            cancellationToken);

        _currentTokenUsage = new Services.Interfaces.TokenUsage(
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

        return ParseDecision(response, toolsManifest);
    }
}
