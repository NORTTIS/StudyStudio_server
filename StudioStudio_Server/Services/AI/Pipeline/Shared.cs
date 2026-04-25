using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using StudioStudio_Server.Services.AI.Models;

#pragma warning disable IDE0130

namespace StudioStudio_Server.Services.AI.Pipeline;

public partial class AIAgent
{
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

    private static JsonObject NormalizeGetPersonalDeadlinesParameters(string userQuestion, JsonObject? parameters)
    {
        var normalizedParameters = parameters ?? new JsonObject();
        var question = NormalizeText(userQuestion);

        if (string.IsNullOrWhiteSpace(question))
        {
            return normalizedParameters;
        }

        static bool HasNumericValue(JsonNode? node)
        {
            if (node is not JsonValue value)
            {
                return false;
            }

            return value.TryGetValue<int>(out _)
                   || value.TryGetValue<long>(out _)
                   || value.TryGetValue<double>(out _)
                   || value.TryGetValue<decimal>(out _);
        }

        if (HasNumericValue(normalizedParameters["days_ahead"]))
        {
            return normalizedParameters;
        }

        var matches = System.Text.RegularExpressions.Regex.Matches(
            question,
            @"\b(\d{1,3})\s*(ngay|day|days)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (matches.Count > 0 && int.TryParse(matches[0].Groups[1].Value, out var daysAhead) && daysAhead > 0)
        {
            normalizedParameters["days_ahead"] = JsonValue.Create(daysAhead);
        }

        return normalizedParameters;
    }

    private static JsonObject NormalizeGetMembersParameters(string userQuestion, JsonObject? parameters)
    {
        var normalizedParameters = parameters ?? new JsonObject();
        var explicitGroupReference = ExtractExplicitGroupReference(userQuestion);

        if (!string.IsNullOrWhiteSpace(explicitGroupReference))
        {
            normalizedParameters["requested_group_reference"] = JsonValue.Create(explicitGroupReference);
        }

        return normalizedParameters;
    }

    private static string? ExtractExplicitGroupReference(string userQuestion)
    {
        var question = NormalizeText(userQuestion);
        if (string.IsNullOrWhiteSpace(question))
        {
            return null;
        }

        var matches = System.Text.RegularExpressions.Regex.Matches(
            question,
            @"\b(?:group|nhom)\s+(?:(?:so|number)\s+)?([a-z0-9][a-z0-9\-_]*)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (!match.Success)
            {
                continue;
            }

            var reference = match.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(reference))
            {
                continue;
            }

            if (reference is "nay" or "hien" or "this" or "current")
            {
                continue;
            }

            return reference;
        }

        return null;
    }

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
            || toolName.Equals("get_personal_group_task", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("get_personal_deadlines", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("get_personal_stats", StringComparison.OrdinalIgnoreCase);
    }

    private static JsonObject EnsureToolParameters(JsonObject? parameters) => parameters ?? new JsonObject();

    private static List<string> ExtractDocumentNamesFromQuestion(string userQuestion)
    {
        if (string.IsNullOrWhiteSpace(userQuestion))
            return new();

        var docNames = new List<string>();

        void AddDocName(string? raw)
        {
            var value = raw?.Trim();
            if (!string.IsNullOrWhiteSpace(value) && !docNames.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                docNames.Add(value);
            }
        }

        // Match strict file token with extension (avoid capturing surrounding words).
        var fileMatches = System.Text.RegularExpressions.Regex.Matches(
            userQuestion,
            @"\b[A-Za-z0-9][A-Za-z0-9_\-\.]*\.(pdf|docx|xlsx|txt|pptx|doc|xls|ppt|jpg|png|jpeg|md|markdown|csv)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (System.Text.RegularExpressions.Match match in fileMatches)
        {
            AddDocName(match.Value);
        }

        if (docNames.Count == 0)
        {
            // Match explicit slug-like names without extension, e.g. team-meeting-notes.
            var slugMatches = System.Text.RegularExpressions.Regex.Matches(
                userQuestion,
                @"\b[A-Za-z0-9]+(?:[-_][A-Za-z0-9]+){1,}(?:\.[A-Za-z0-9]+)?\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (System.Text.RegularExpressions.Match match in slugMatches)
            {
                AddDocName(match.Value);
            }
        }

        if (docNames.Count == 0)
        {
            // Support quoted file names, including names with spaces.
            var quotedMatches = System.Text.RegularExpressions.Regex.Matches(
                userQuestion,
                "[\\\"'`](?<name>[^\\\"'`]{2,})[\\\"'`]",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (System.Text.RegularExpressions.Match match in quotedMatches)
            {
                if (match.Groups["name"].Success)
                {
                    AddDocName(match.Groups["name"].Value);
                }
            }
        }

        if (docNames.Count == 0)
        {
            var keywordPatterns = new[]
            {
                @"file\s+([^\s,]+)",
                @"document\s+([^\s,]+)",
                @"tai\s+lieu\s+([^\s,]+)",
                @"tài\s+liệu\s+([^\s,]+)"
            };

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
                        AddDocName(m.Groups[1].Value);
                    }
                }
            }
        }

        return docNames;
    }

    private static bool HasExplicitDocumentName(string userQuestion) =>
        ExtractDocumentNamesFromQuestion(userQuestion).Count > 0;

    private static bool TryResolveRequestedDocumentId(
        string userQuestion,
        AIQueryResult docListResult,
        out string? documentId,
        out string? matchedDocumentName)
    {
        documentId = null;
        matchedDocumentName = null;

        var mentionedDocNames = ExtractDocumentNamesFromQuestion(userQuestion);
        if (mentionedDocNames.Count == 0)
        {
            return false;
        }

        var matchedIds = MatchDocumentNamesAndExtractIds(mentionedDocNames, docListResult);
        if (matchedIds.Count == 0)
        {
            matchedDocumentName = mentionedDocNames[0];
            return false;
        }

        documentId = matchedIds[0];
        matchedDocumentName = mentionedDocNames[0];
        return true;
    }

    private static List<string> MatchDocumentNamesAndExtractIds(
        List<string> searchNames,
        AIQueryResult docListResult)
    {
        if (searchNames.Count == 0 || !docListResult.IsSuccess || docListResult.Data == null)
            return new();

        var matchedIds = new List<string>();

        if (!docListResult.Data.TryGetPropertyValue("documents", out var docsNode) || docsNode is not JsonArray docs)
        {
            return new();
        }

        foreach (var searchName in searchNames)
        {
            var searchNameLower = searchName.ToLower();
            var candidates = new List<(string DocumentId, DateTime CreatedAt)>();

            foreach (var doc in docs)
            {
                if (doc is not JsonObject docObj || !docObj.TryGetPropertyValue("file_name", out var fileNameNode) || fileNameNode == null)
                    continue;

                var fileName = fileNameNode.ToString().ToLowerInvariant();
                var extensionIndex = fileName.LastIndexOf('.');
                var fileNameWithoutExtension = extensionIndex > 0
                    ? fileName[..extensionIndex]
                    : fileName;

                if (fileName.Equals(searchNameLower) ||
                    fileName.Contains(searchNameLower) ||
                    searchNameLower.Contains(fileNameWithoutExtension))
                {
                    string? docId = null;
                    if (docObj.TryGetPropertyValue("document_id", out var idNode))
                    {
                        var raw = idNode?.GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out var parsed))
                        {
                            docId = parsed.ToString();
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(docId))
                    {
                        candidates.Add((docId, ExtractDocumentCreatedAt(docObj)));
                    }
                }
            }

            var bestCandidate = candidates
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(bestCandidate.DocumentId) && !matchedIds.Contains(bestCandidate.DocumentId))
            {
                matchedIds.Add(bestCandidate.DocumentId);
            }
        }

        return matchedIds;
    }

    private static DateTime ExtractDocumentCreatedAt(JsonObject docObj)
    {
        if (TryParseDocumentTimestamp(docObj["created_at"], out var createdAt))
        {
            return createdAt;
        }

        if (TryParseDocumentTimestamp(docObj["uploaded_at"], out var uploadedAt))
        {
            return uploadedAt;
        }

        return DateTime.MinValue;
    }

    private static bool TryParseDocumentTimestamp(JsonNode? node, out DateTime timestamp)
    {
        timestamp = default;
        if (node == null)
        {
            return false;
        }

        var raw = node.GetValue<string>();
        return !string.IsNullOrWhiteSpace(raw)
            && DateTime.TryParse(raw, out timestamp);
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
            CompactTasksForPrompt(compactData);
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

    private static void CompactTasksForPrompt(JsonObject data)
    {
        if (!data.TryGetPropertyValue("tasks", out var tasksNode) || tasksNode is not JsonArray tasks)
        {
            return;
        }

        const int maxTasksForPrompt = 20;
        var slimTasks = new JsonArray();
        var keepCount = Math.Min(tasks.Count, maxTasksForPrompt);

        for (var i = 0; i < keepCount; i++)
        {
            if (tasks[i] is not JsonObject taskObj)
            {
                continue;
            }

            // Keep only the fields needed for table rendering in final response.
            slimTasks.Add(new JsonObject
            {
                ["title"] = taskObj["title"]?.DeepClone(),
                ["status"] = taskObj["status"]?.DeepClone(),
                ["priority"] = taskObj["priority"]?.DeepClone(),
                ["severity"] = taskObj["severity"]?.DeepClone(),
                ["assignee_name"] = taskObj["assignee_name"]?.DeepClone(),
                ["due_date"] = taskObj["due_date"]?.DeepClone(),
                ["is_overdue"] = taskObj["is_overdue"]?.DeepClone(),
                ["is_completed"] = taskObj["is_completed"]?.DeepClone()
            });
        }

        data["tasks"] = slimTasks;
        if (tasks.Count > keepCount)
        {
            data["tasks_truncated_count"] = JsonValue.Create(tasks.Count - keepCount);
        }
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
                    var maxItemsForThisArray = key.Equals("tasks", StringComparison.OrdinalIgnoreCase)
                        ? 20
                        : MaxArrayItemsForPrompt;

                    var removedCount = arr.Count - maxItemsForThisArray;
                    while (arr.Count > maxItemsForThisArray)
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
            ? "You are an AI assistant that turns tool data into a clean Markdown answer. Never return JSON. If the answer contains a task list, render it as a Markdown table (columns: Task, Status, Priority, Severity, Assignee, Due date). If the answer contains a group member list, render it as a Markdown table (columns: Member, Role). Show pagination status only when tool results explicitly provide pagination fields. Include a short \"Next steps\" section only when the data implies concrete actions."
            : "Ban la tro ly AI bien du lieu tu tool thanh cau tra loi Markdown sach se. Tuyet doi khong tra ve JSON. Neu co danh sach cong viec, bat buoc trinh bay bang bang Markdown (cot: Cong viec, Trang thai, Uu tien, Muc do, Nguoi phu trach, Han chot). Neu co danh sach thanh vien nhom, trinh bay bang bang Markdown (cot: Thanh vien, Vai tro). Chi neu thong tin phan trang khi tool tra ve day du current_page/total_pages/has_next_page. Chi them muc \"Goi y tiep theo\" khi du lieu thuc su goi y hanh dong cu the.";
    }

    private void AppendToolSpecificAnswerHints(StringBuilder promptBuilder, ToolExecutionHistory history)
    {
        var toolHints = history.Calls
            .Select(call => _toolRegistry.GetTool(call.ToolName))
            .Where(tool => tool != null)
            .DistinctBy(tool => tool!.Name, StringComparer.OrdinalIgnoreCase)
            .Where(tool => !string.IsNullOrWhiteSpace(tool!.AnswerStyleHint) || !string.IsNullOrWhiteSpace(tool.OutputFormatHint))
            .ToList();

        if (toolHints.Count == 0)
        {
            return;
        }

        promptBuilder.AppendLine("=== TOOL-SPECIFIC ANSWER HINTS ===");
        foreach (var tool in toolHints)
        {
            promptBuilder.AppendLine($"Tool: {tool!.Name}");
            if (!string.IsNullOrWhiteSpace(tool.AnswerStyleHint))
            {
                promptBuilder.AppendLine($"AnswerStyleHint: {tool.AnswerStyleHint}");
            }

            if (!string.IsNullOrWhiteSpace(tool.OutputFormatHint))
            {
                promptBuilder.AppendLine($"OutputFormatHint: {tool.OutputFormatHint}");
            }

            promptBuilder.AppendLine();
        }

        promptBuilder.AppendLine("- Prefer these tool-specific hints when phrasing the final answer.");
        promptBuilder.AppendLine("- Never let style hints override the actual tool data.");
        promptBuilder.AppendLine();
    }

    private string BuildPromptForSynthesis(string draftAnswer, ToolExecutionHistory history)
    {
        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine("=== DRAFT ANSWER ===");
        promptBuilder.AppendLine(draftAnswer);
        promptBuilder.AppendLine();

        if (history.Calls.Count > 0)
        {
            promptBuilder.AppendLine("=== TOOL RESULTS (FOR FACT CHECK) ===");
            var recentCalls = history.Calls.TakeLast(5).ToList();
            foreach (var call in recentCalls)
            {
                var resultForSynthesis = call.ToolName.Equals("search_documents", StringComparison.OrdinalIgnoreCase)
                    ? call.Result.ToJson()
                    : BuildCompactToolResultForPrompt(call.Result);

                // Keep near-full document evidence for search_documents, but still cap to protect context window.
                if (resultForSynthesis.Length > 12000)
                {
                    resultForSynthesis = resultForSynthesis[..12000] + "... [truncated]";
                }

                promptBuilder.AppendLine($"Tool: {call.ToolName}");
                promptBuilder.AppendLine($"Parameters: {call.Parameters}");
                promptBuilder.AppendLine($"Result: {resultForSynthesis}");
                promptBuilder.AppendLine();
            }
        }

        AppendToolSpecificAnswerHints(promptBuilder, history);
        promptBuilder.AppendLine("=== INSTRUCTIONS ===");
        promptBuilder.AppendLine("- Rewrite the draft answer into a clean final answer.");
        promptBuilder.AppendLine("- Keep the answer faithful to the tool data.");
        promptBuilder.AppendLine("- Expand the answer beyond one short paragraph when data is available: include key findings, supporting details, and caveats.");
        promptBuilder.AppendLine("- If tool data includes metrics, numbers, dates, file names, or statuses, explicitly include the most relevant ones.");
        promptBuilder.AppendLine("- Keep the response concise only when tool data is minimal.");
        promptBuilder.AppendLine("- Remove vague filler phrases such as acknowledgements that do not add information.");
        promptBuilder.AppendLine("- Only show pagination when the tool result explicitly contains pagination fields (current_page/total_pages/has_next_page).");
        promptBuilder.AppendLine("- If those fields are absent, do NOT output pagination lines such as 'Trang hien tai: ...' or 'Co trang tiep theo: ...', and do not infer default values like 1/1.");

        var hasSearchDocumentData = history.Calls.Any(c =>
            c.ToolName.Equals("search_documents", StringComparison.OrdinalIgnoreCase)
            && c.Result.IsSuccess
            && c.Result.Data != null
            && c.Result.Data.TryGetPropertyValue("documents", out var docsNode)
            && docsNode is JsonArray docsArr
            && docsArr.Count > 0);

        if (hasSearchDocumentData)
        {
            promptBuilder.AppendLine("- DOCUMENT DETAIL MODE (IMPORTANT):");
            promptBuilder.AppendLine("- If the user asks for details/content of a document, do not over-summarize.");
            promptBuilder.AppendLine("- Cover the retrieved content comprehensively:");
            promptBuilder.AppendLine("- Preserve concrete facts from tool results (dates, attendees, statuses, deadlines, named actions).");
            promptBuilder.AppendLine("- If a section is not present in retrieved content, state that clearly instead of skipping silently.");
        }
        return promptBuilder.ToString();
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

    private string BuildPromptForFinalAnswer(
        string userQuestion,
        ToolExecutionHistory history)
    {
        var promptBuilder = new StringBuilder();

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

        AppendToolSpecificAnswerHints(promptBuilder, history);

        promptBuilder.AppendLine("=== INSTRUCTIONS ===");
        promptBuilder.AppendLine("- Based on the tool results above, provide a clear and helpful answer");
        promptBuilder.AppendLine("- Format your answer in Vietnamese with proper markdown if needed");
        promptBuilder.AppendLine("- Do not answer in only one or two short lines when tool data is available. Cover key points and supporting evidence.");
        promptBuilder.AppendLine("- Prioritize completeness and factual grounding over brevity.");
        promptBuilder.AppendLine("- If task list data exists, present tasks as a markdown table with columns: Cong viec, Trang thai, Uu tien, Muc do, Nguoi phu trach, Han chot.");
        promptBuilder.AppendLine("- If group member list data exists, present members as a markdown table with columns: Thanh vien, Vai tro.");
        promptBuilder.AppendLine("- Only show pagination when the tool result explicitly contains pagination fields (current_page/total_pages/has_next_page).");
        promptBuilder.AppendLine("- If those fields are absent, do NOT output pagination lines such as 'Trang hien tai: ...' or 'Co trang tiep theo: ...', and do not infer default values like 1/1.");
        promptBuilder.AppendLine("- For deadline responses, separate upcoming_deadlines and overdue_tasks clearly. If total_upcoming = 0 but total_overdue > 0, state both facts explicitly to avoid contradiction.");
        promptBuilder.AppendLine("- If tool results are empty or insufficient, say so honestly");

        return promptBuilder.ToString();
    }
}

