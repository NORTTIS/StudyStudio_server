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
        var hasLetterOrDigit = normalizedQuestion.Any(char.IsLetterOrDigit);
        var letterOrDigitCount = normalizedQuestion.Count(char.IsLetterOrDigit);
        var punctuationCount = normalizedQuestion.Count(ch => !char.IsLetterOrDigit(ch) && !char.IsWhiteSpace(ch));
        var tokenCount = normalizedQuestion
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length;

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

        var looksLikeNoise = !string.IsNullOrWhiteSpace(userQuestion)
            && (!hasLetterOrDigit
                || (letterOrDigitCount <= 6 && punctuationCount >= 2)
                || (tokenCount <= 1 && !isTaskIntent && !isDocumentIntent && !isFollowUp && punctuationCount > 0));

        var isUnclearIntent = !isTaskIntent && !isDocumentIntent && !isFollowUp && looksLikeNoise;
        var requiresTool = !isUnclearIntent;
        var summary = $"category={category}, taskIntent={isTaskIntent}, documentIntent={isDocumentIntent}, followUp={isFollowUp}, unclear={isUnclearIntent}";

        return new AIIntentAnalysis(category, requiresTool, isTaskIntent, isDocumentIntent, isFollowUp, isUnclearIntent, summary);
    }

    private static string BuildClarificationQuestion(AIQueryContext context)
    {
        return context.Language.Equals("en", StringComparison.OrdinalIgnoreCase)
            ? "I couldn't determine your intent. Do you want to ask about personal tasks, deadlines, productivity stats, or group-assigned tasks?"
            : "Mình chưa xác định được ý bạn. Bạn muốn hỏi về công việc cá nhân, deadline, thống kê năng suất, hay các task được giao từ group?";
    }

    private static bool LooksLikePromptLeakage(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = NormalizeText(text);

        return normalized.Contains("tro ly ai bien du lieu tu tool thanh cau tra loi markdown", StringComparison.Ordinal)
               || normalized.Contains("you are an ai assistant that turns tool data into a clean markdown answer", StringComparison.Ordinal)
               || normalized.Contains("tuyet doi khong tra ve json", StringComparison.Ordinal)
               || normalized.Contains("never return json", StringComparison.Ordinal);
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

    private static AIQueryResult? GetLatestSuccessfulToolResult(ToolExecutionHistory history, string toolName)
    {
        return history.Calls
            .Where(c => c.ToolName.Equals(toolName, StringComparison.OrdinalIgnoreCase) && c.Result.IsSuccess)
            .OrderByDescending(c => c.ExecutedAt)
            .Select(c => c.Result)
            .FirstOrDefault();
    }

    private static bool IsNamedDocumentSearchWithoutResolvedId(string userQuestion, AgentDecision decision, JsonObject parameters)
    {
        if (!string.Equals(decision.ToolName, "search_documents", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!HasExplicitDocumentName(userQuestion))
        {
            return false;
        }

        var rawDocumentId = parameters.TryGetPropertyValue("document_id", out var node)
            ? node?.GetValue<string>()
            : null;

        return !Guid.TryParse(rawDocumentId, out _);
    }

    private static bool IsStudioGroupTool(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return false;
        }

        return toolName.Equals("get_tasks", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("get_group_stats", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("get_members", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("get_deadlines", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("search_documents", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("get_group_performance", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("get_group_documents", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("get_group_risk", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadGuidParameter(JsonObject parameters, string key, out Guid value)
    {
        value = Guid.Empty;
        if (!parameters.TryGetPropertyValue(key, out var node) || node == null)
        {
            return false;
        }

        var raw = node.GetValue<string>();
        return Guid.TryParse(raw, out value);
    }

    private static bool ContainsExactGroupName(string question, string groupName)
    {
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(groupName))
        {
            return false;
        }

        var startIndex = 0;
        while (true)
        {
            var idx = question.IndexOf(groupName, startIndex, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return false;
            }

            var beforeOk = idx == 0 || !char.IsLetterOrDigit(question[idx - 1]);
            var afterIndex = idx + groupName.Length;
            var afterOk = afterIndex >= question.Length || !char.IsLetterOrDigit(question[afterIndex]);

            if (beforeOk && afterOk)
            {
                return true;
            }

            startIndex = idx + 1;
        }
    }

    private static bool TryResolveGroupIdFromStudioGroups(
        string userQuestion,
        ToolExecutionHistory history,
        out Guid groupId)
    {
        groupId = Guid.Empty;

        // Priority 1: user explicitly provided GUID in question
        var guidMatches = System.Text.RegularExpressions.Regex.Matches(
            userQuestion,
            @"\b[0-9a-fA-F]{8}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{12}\b");
        if (guidMatches.Count > 0 && Guid.TryParse(guidMatches[0].Value, out var parsedGuid))
        {
            groupId = parsedGuid;
            return true;
        }

        var groupsResult = GetLatestSuccessfulToolResult(history, "get_studio_groups");
        if (groupsResult?.Data == null)
        {
            return false;
        }

        if (!groupsResult.Data.TryGetPropertyValue("groups", out var groupsNode) || groupsNode is not JsonArray groupsArr)
        {
            return false;
        }

        var candidates = new List<Guid>();

        foreach (var groupNode in groupsArr)
        {
            if (groupNode is not JsonObject groupObj)
            {
                continue;
            }

            var idRaw = groupObj["id"]?.GetValue<string>();
            var nameRaw = groupObj["name"]?.GetValue<string>();
            if (!Guid.TryParse(idRaw, out var candidateId) || string.IsNullOrWhiteSpace(nameRaw))
            {
                continue;
            }

            if (ContainsExactGroupName(userQuestion, nameRaw))
            {
                candidates.Add(candidateId);
            }
        }

        if (candidates.Count != 1)
        {
            return false;
        }

        groupId = candidates[0];
        return true;
    }

    private static bool HasMultipleExplicitGroupReferences(string userQuestion)
    {
        if (string.IsNullOrWhiteSpace(userQuestion))
        {
            return false;
        }

        var matches = System.Text.RegularExpressions.Regex.Matches(
            userQuestion,
            @"\b(?:group|nhom)\s+(?:(?:so|number)\s+)?([a-zA-Z0-9][a-zA-Z0-9\-_]*)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (!match.Success || match.Groups.Count < 2)
            {
                continue;
            }

            var value = match.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                refs.Add(value);
            }
        }

        return refs.Count > 1;
    }

    private async Task<AIFlowDecision> ReviewPlannedToolAsync(string userQuestion, AIQueryContext context, AgentDecision decision, ToolExecutionHistory history, CancellationToken cancellationToken)
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

        if (decision.ToolName.Equals("get_personal_deadlines", StringComparison.OrdinalIgnoreCase))
        {
            reviewedParameters = NormalizeGetPersonalDeadlinesParameters(userQuestion, reviewedParameters);
        }

        if (decision.ToolName.Equals("get_members", StringComparison.OrdinalIgnoreCase))
        {
            reviewedParameters = NormalizeGetMembersParameters(userQuestion, reviewedParameters);
        }

        if (decision.ToolName.Equals("search_documents", StringComparison.OrdinalIgnoreCase)
            && context.GroupId.HasValue
            && HasExplicitDocumentName(userQuestion))
        {
            if (!reviewedParameters.TryGetPropertyValue("document_id", out var documentIdNode)
                || documentIdNode == null
                || string.IsNullOrWhiteSpace(documentIdNode.GetValue<string>()))
            {
                var mentionedDocNames = ExtractDocumentNamesFromQuestion(userQuestion);
                if (mentionedDocNames.Count > 0)
                {
                    reviewedParameters["document_id"] = JsonValue.Create(mentionedDocNames[0]);
                }
            }
        }

        if (decision.ToolName.Equals("search_documents", StringComparison.OrdinalIgnoreCase))
        {
            if (!reviewedParameters.TryGetPropertyValue("query", out var queryNode)
                || queryNode == null
                || string.IsNullOrWhiteSpace(queryNode.GetValue<string>()))
            {
                reviewedParameters["query"] = JsonValue.Create(userQuestion.Trim());
            }
        }

        if (context.StudioId.HasValue
            && !context.GroupId.HasValue
            && IsStudioGroupTool(decision.ToolName))
        {
            if (HasMultipleExplicitGroupReferences(userQuestion))
            {
                return new AIFlowDecision(
                    StepName: "parameter-review",
                    Decision: decision,
                    ToolParameters: reviewedParameters,
                    IsAccepted: false,
                    ReviewState: "needs_fix",
                    ReviewNote: "[needs_fix] He thong chi ho tro 1 group cho moi lan goi group tool. Ban dang chi dinh nhieu group, hay chon 1 group duy nhat.",
                    SuggestedToolName: decision.ToolName);
            }

            if (reviewedParameters.TryGetPropertyValue("group_ids", out var groupIdsNode)
                && groupIdsNode is JsonArray groupIdsArray
                && groupIdsArray.Count > 1)
            {
                return new AIFlowDecision(
                    StepName: "parameter-review",
                    Decision: decision,
                    ToolParameters: reviewedParameters,
                    IsAccepted: false,
                    ReviewState: "needs_fix",
                    ReviewNote: "[needs_fix] Group-level tools chi nhan 1 group_id, khong ho tro nhieu group_ids.",
                    SuggestedToolName: decision.ToolName);
            }

            if (!TryReadGuidParameter(reviewedParameters, "group_id", out _))
            {
                if (TryResolveGroupIdFromStudioGroups(userQuestion, history, out var resolvedGroupId))
                {
                    reviewedParameters["group_id"] = JsonValue.Create(resolvedGroupId.ToString());
                    _logger.LogInformation(
                        "[PARAM-REVIEW] Resolved group_id={GroupId} for tool={Tool} from get_studio_groups + user question",
                        resolvedGroupId,
                        decision.ToolName);
                }
                else
                {
                    var hasStudioGroups = history.Calls.Any(c =>
                        c.ToolName.Equals("get_studio_groups", StringComparison.OrdinalIgnoreCase) && c.Result.IsSuccess);

                    return new AIFlowDecision(
                        StepName: "parameter-review",
                        Decision: decision,
                        ToolParameters: reviewedParameters,
                        IsAccepted: false,
                        ReviewState: "needs_fix",
                        ReviewNote: hasStudioGroups
                            ? "[needs_fix] Khong tim thay group nao khop chinh xac 100% voi ten group trong cau hoi. Hay nhap dung exact group name hoac group_id."
                            : "[needs_fix] Group tool in Studio scope requires group_id. Goi get_studio_groups truoc de lay danh sach group va id.",
                        SuggestedToolName: hasStudioGroups ? decision.ToolName : "get_studio_groups");
                }
            }
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
            if (!context.StudioId.HasValue)
            {
                promptBuilder.AppendLine("- If the user asks for a generic task list ('task list', 'all tasks', 'group tasks', 'show tasks in this group'), call get_tasks with query/search empty and no filter fields unless a filter is explicitly requested.");
            }
            else
            {
                promptBuilder.AppendLine("- STUDIO SCOPE RULE: for any Group-level tool call (get_tasks/get_group_stats/get_members/get_deadlines/get_group_performance/get_group_documents/get_group_risk/search_documents), you MUST include a valid group_id GUID.");
                promptBuilder.AppendLine("- If group_id is not known yet, call get_studio_groups first, then map the user-mentioned group name to its id and call the Group-level tool with that group_id.");
            }
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
            if (!context.StudioId.HasValue)
            {
                promptBuilder.AppendLine("  * {\"tool_name\":\"get_tasks\",\"parameters\":{}} for a generic list request.");
                promptBuilder.AppendLine("  * {\"tool_name\":\"get_tasks\",\"parameters\":{\"status_category\":\"Completed\"}} for completed tasks.");
                promptBuilder.AppendLine("  * {\"tool_name\":\"get_tasks\",\"parameters\":{\"min_priority\":\"Medium\"}} for medium and above priority.");
                promptBuilder.AppendLine("  * {\"tool_name\":\"get_tasks\",\"parameters\":{\"min_priority\":\"High\"}} for high and above priority.");
                promptBuilder.AppendLine("  * {\"tool_name\":\"get_tasks\",\"parameters\":{\"min_severity\":\"Major\"}} for high severity and above.");
            }
            else
            {
                promptBuilder.AppendLine("  * {\"tool_name\":\"get_studio_groups\",\"parameters\":{}} to fetch groups and ids.");
                promptBuilder.AppendLine("  * {\"tool_name\":\"get_tasks\",\"parameters\":{\"group_id\":\"<group-guid>\"}} for a group's task list.");
                promptBuilder.AppendLine("  * {\"tool_name\":\"get_tasks\",\"parameters\":{\"group_id\":\"<group-guid>\",\"status_category\":\"Completed\"}} for completed tasks in that group.");
            }
            promptBuilder.AppendLine("- Chi hien thi thong tin phan trang khi ket qua tool THUC SU co cac truong current_page/total_pages/has_next_page.");
            promptBuilder.AppendLine("- Neu ket qua tool khong co cac truong phan trang, TUYET DOI khong tu them dong 'Trang hien tai: ...' hoac 'Co trang tiep theo: ...' va khong duoc suy dien 1/1.");
            promptBuilder.AppendLine("- IMPORTANT - JSON FORMAT: Your response MUST be a valid single-line JSON object.");
            promptBuilder.AppendLine("- Tool execution policy: a tool may be retried only if its previous call failed. Never call the same tool again after it has succeeded in the current turn.");
            promptBuilder.AppendLine("  - Tool call: {\"action\": \"tool_call\", \"tool_name\": \"tool_name_here\", \"parameters\": {\"key\": \"value\"}}");
            promptBuilder.AppendLine("  - Final answer: {\"action\": \"answer\", \"final_answer\": \"your answer text here\"}");
            promptBuilder.AppendLine("  - NEVER omit the parameters field. If tool needs no params, use {\"parameters\": {}}");
            promptBuilder.AppendLine("- final_answer: chi la van ban thuan tuy. Khong dat trong ```, khong dat trong JSON object. Neu can xuong dong, dung \\n. Khong dung danh sach bullet dac biet.");
            promptBuilder.AppendLine("- DOCUMENT SEARCH STRATEGY:");
            promptBuilder.AppendLine("  * search_documents: For finding CONTENT within documents. Use with: query (required), document_id (optional), top_k (optional).");
            promptBuilder.AppendLine("  * If user asks about document CONTENT (\"what is...\", \"find...\", \"search in...\") -> use search_documents with semantic query.");
            promptBuilder.AppendLine("  * If user mentions specific file like \"2003.txt\" -> call search_documents directly and pass that filename in document_id.");
            promptBuilder.AppendLine("  * document_id can be GUID or filename. The tool will resolve filename to the latest uploaded attachment in DB.");
            promptBuilder.AppendLine("  * If query is missing, reuse the full user question as query.");
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
            promptBuilder.AppendLine("- Chi hien thi thong tin phan trang khi ket qua tool THUC SU co cac truong current_page/total_pages/has_next_page.");
            promptBuilder.AppendLine("- Neu ket qua tool khong co cac truong phan trang, TUYET DOI khong tu them dong 'Trang hien tai: ...' hoac 'Co trang tiep theo: ...' va khong duoc suy dien 1/1.");
            promptBuilder.AppendLine("- IMPORTANT - JSON FORMAT: Your response MUST be a valid single-line JSON object.");
            promptBuilder.AppendLine("- Tool execution policy: a tool may be retried only if its previous call failed. Never call the same tool again after it has succeeded in the current turn.");
            promptBuilder.AppendLine("  - Tool call: {\"action\": \"tool_call\", \"tool_name\": \"tool_name_here\", \"parameters\": {\"key\": \"value\"}}");
            promptBuilder.AppendLine("  - Final answer: {\"action\": \"answer\", \"final_answer\": \"your answer text here\"}");
            promptBuilder.AppendLine("  - NEVER omit the parameters field. If tool needs no params, use {\"parameters\": {}}");
            promptBuilder.AppendLine("- final_answer: chi la van ban thuan tuy. Khong dat trong ```, khong dat trong JSON object. Neu can xuong dong, dung \\n. Khong dung danh sach bullet dac biet.");

            if (history.Calls.Any(c => c.ToolName == "search_documents" && !c.Result.IsSuccess))
            {
                promptBuilder.AppendLine("- PREVIOUS FAILURE: search_documents failed due to invalid/missing params. "
                    + "Retry search_documents with a valid semantic query taken from the user question. "
                    + "Do not call get_group_documents as an automatic fallback.");
            }

            bool hasCalledGetTasks = history.Calls.Any(c => c.ToolName == "get_tasks" && c.Result.IsSuccess);
            if (!hasCalledGetTasks)
            {
                if (!context.StudioId.HasValue)
                {
                    promptBuilder.AppendLine("- IMPORTANT: Neu cau hoi lien quan den CONG VIEC (task, deadline, tien do, "
                        + "thanh vien, diem score) ma CHUA goi get_tasks -> GOI GET_TASKS NGAY. "
                        + "Ket qua tu documents (get_group_documents/search_documents) KHONG phai la task data. "
                        + "Phai goi get_tasks de lay danh sach cong viec.");
                }
                else
                {
                    promptBuilder.AppendLine("- IMPORTANT (Studio scope): Neu cau hoi lien quan den cong viec cua mot group cu the, goi get_tasks voi group_id GUID da map tu get_studio_groups. KHONG goi get_tasks voi parameters rong.");
                }
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
